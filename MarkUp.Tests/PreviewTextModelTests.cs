using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using System.Text.RegularExpressions;
using MarkUp.Core;

namespace MarkUp.Tests;

/// <summary>
/// Guards the contract between the host-side <see cref="MarkdownSelectionProjection"/> and
/// the preview page's JavaScript text model: both must describe the rendered document as the
/// same string (visible text with exactly one '\n' between consecutive blocks). The test-side
/// simulator below applies the same rules the preview script does to the HTML fragment the
/// parser emits, so any drift between the two models fails here instead of as an off-by-one
/// caret in the preview pane.
/// </summary>
[TestClass]
public sealed class PreviewTextModelTests
{
    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
        { "p", "h1", "h2", "h3", "h4", "h5", "h6", "li", "blockquote", "pre", "tr", "div" };

    private static readonly HashSet<string> StructuralTags = new(StringComparer.OrdinalIgnoreCase)
        { "body", "ul", "ol", "table", "thead", "tbody", "tr", "blockquote", "editor-body" };

    /// <summary>Mirrors buildTextMap() in the preview script.</summary>
    private static string SimulatePreviewTextModel(string fragment)
    {
        var sb = new StringBuilder();
        var open = new List<string>(); // element stack
        string? prevBlock = null;
        var hasPrev = false;
        var blockIds = new Dictionary<int, string>(); // stack depth -> unique block id
        var counter = 0;

        var pos = 0;
        while (pos < fragment.Length)
        {
            if (fragment[pos] == '<')
            {
                var close = fragment.IndexOf('>', pos);
                var tag = fragment[(pos + 1)..close];
                pos = close + 1;
                if (tag.StartsWith('/'))
                {
                    open.RemoveAt(open.Count - 1);
                    continue;
                }
                var selfClosing = tag.EndsWith('/');
                var name = tag.Split(' ', '/', '\t')[0];
                if (selfClosing) continue;
                open.Add(name);
                blockIds[open.Count - 1] = $"{name}#{counter++}";
                continue;
            }

            var next = fragment.IndexOf('<', pos);
            if (next < 0) next = fragment.Length;
            var text = System.Net.WebUtility.HtmlDecode(fragment[pos..next]);
            pos = next;

            var parent = open.Count > 0 ? open[^1] : "editor-body";
            if (text.Trim().Length == 0 && (parent == "editor-body" || StructuralTags.Contains(parent)))
                continue;

            string? block = null;
            for (var d = open.Count - 1; d >= 0; d--)
            {
                if (BlockTags.Contains(open[d])) { block = blockIds[d]; break; }
            }

            if (hasPrev && block != prevBlock) sb.Append('\n');
            sb.Append(text);
            prevBlock = block;
            hasPrev = true;
        }

        return sb.ToString();
    }

    private static void AssertModelsAgree(string markdown)
    {
        var fragment = MarkdownParser.ToHtmlFragment(markdown);
        var domModel = SimulatePreviewTextModel(fragment);
        var projection = MarkdownSelectionProjection.Create(markdown).VisibleText;
        Assert.AreEqual(projection, domModel,
            $"Projection and preview text models differ for:\n{markdown}\nfragment:\n{fragment}");
    }

    [TestMethod]
    public void Fragment_HasNoWhitespaceBetweenBlockTags()
    {
        var md = "# H\n\npara\n\n- a\n- b\n\n1. x\n\n> q\n\n| A | B |\n|---|---|\n| 1 | 2 |\n\n```\ncode\n```\n\n---\n\n- [ ] t";
        var fragment = MarkdownParser.ToHtmlFragment(md);
        Assert.IsFalse(Regex.IsMatch(fragment, @">\s+<"), $"Whitespace between tags in:\n{fragment}");
    }

    [TestMethod]
    public void Models_Agree_ForParagraphsAndHeadings()
        => AssertModelsAgree("# Title\n\nFirst para\nsoft break\n\n## Sub\n\nSecond **bold** and *em* `code` [link](u) ![img](p)");

    [TestMethod]
    public void Models_Agree_ForLists()
        => AssertModelsAgree("Intro\n\n- one\n- two **b**\n\n1. first\n2. second\n\nOutro");

    [TestMethod]
    public void Models_Agree_ForTaskLists()
        => AssertModelsAgree("- [ ] open\n- [x] done\n\nAfter");

    [TestMethod]
    public void Models_Agree_ForTables()
        => AssertModelsAgree("| A | B |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |\n\nAfter table");

    [TestMethod]
    public void Models_Agree_ForCodeBlocks()
        => AssertModelsAgree("Before\n\n```cs\nvar x = 1;\nvar y = 2;\n```\n\nAfter");

    [TestMethod]
    public void Models_Agree_ForBlockquotes()
        => AssertModelsAgree("> quoted line\n> second line\n\nNormal");

    [TestMethod]
    public void Models_Agree_ForHorizontalRulesAndSetext()
        => AssertModelsAgree("Title\n=====\n\ntext\n\n---\n\nSub\n---\n\nmore");

    [TestMethod]
    public void Models_Agree_ForMixedDocument()
        => AssertModelsAgree(
            "# Doc\n\nIntro paragraph.\n\n## List\n\n- a\n- b\n\n## Tasks\n\n- [ ] t1\n- [x] t2\n\n" +
            "## Table\n\n| h1 | h2 |\n|:--|--:|\n| c1 | c2 |\n\n## Code\n\n```\nline\n```\n\n> quote\n\nEnd.");

    [TestMethod]
    public void TaskCheckbox_IsClickableAndIndexed()
    {
        var fragment = MarkdownParser.ToHtmlFragment("- [ ] a\n- [x] b\n\n> - [ ] c");
        Assert.IsFalse(fragment.Contains("disabled"), "checkboxes must not be disabled");
        Assert.IsTrue(fragment.Contains("data-task-index=\"0\""));
        Assert.IsTrue(fragment.Contains("data-task-index=\"1\" contenteditable=\"false\" checked"));
        Assert.IsTrue(fragment.Contains("data-task-index=\"2\""), "blockquoted tasks continue the index");
        Assert.IsTrue(fragment.Contains("/>a</li>"), "no whitespace between checkbox and item text");
    }

    [TestMethod]
    public void LinkBullet_IsAnOrdinaryListItem_NotATask()
    {
        var fragment = MarkdownParser.ToHtmlFragment("- [link](http://x)");
        Assert.IsFalse(fragment.Contains("task-list"));
        Assert.IsTrue(fragment.Contains("<li><a href=\"http://x\">link</a></li>"));
    }

    [TestMethod]
    public void BareTaskMarkerWithoutText_IsATask()
    {
        var fragment = MarkdownParser.ToHtmlFragment("- [ ]");
        Assert.IsTrue(fragment.Contains("task-list"));
    }

    [TestMethod]
    public void ToHtml_WithBaseHref_EmitsBaseTag()
    {
        var html = MarkdownParser.ToHtml("x", baseHref: "https://markup.local/");
        Assert.IsTrue(html.Contains("<base href=\"https://markup.local/\" />"));
        Assert.IsFalse(MarkdownParser.ToHtml("x").Contains("<base "));
        Assert.IsTrue(MarkdownParser.ToHtmlForPrint("x", "t", "https://markup.local/").Contains("<base href="));
    }

    [TestMethod]
    public void EditablePage_ExposesPreviewScriptingApi()
    {
        var html = MarkdownParser.ToHtml("x", editable: true);
        foreach (var fn in new[] { "function updateContent", "function setMirroredCaret", "function setMirroredSelection",
                     "function getSelectionOffsets", "function setSelectionOffsets", "function setScrollRatio",
                     "function setZoomLevel", "function getMirroredText", "function selectPreviewText", "'taskToggle'" })
        {
            Assert.IsTrue(html.Contains(fn), $"missing {fn}");
        }
    }

    [TestMethod]
    public void NonEditablePage_DisablesCheckboxInteraction()
    {
        var html = MarkdownParser.ToHtml("- [ ] a", editable: false);
        Assert.IsTrue(html.Contains("input[type=checkbox] { pointer-events: none; }"));
        Assert.IsFalse(html.Contains("taskToggle"));
    }

    [TestMethod]
    public void Projection_BinarySearch_MatchesEveryCaretPosition()
    {
        // Exhaustively map every source caret in a mixed document and check the mapping is
        // monotonic and bounded — the binary-search rewrite must behave like the old scan.
        var md = "# Title\n\nSome **bold** text\n\n- a\n- [ ] b\n\n| x | y |\n|---|---|\n| 1 | 2 |\n\n```\nc\n```\n";
        var projection = MarkdownSelectionProjection.Create(md);
        var last = 0;
        for (var i = 0; i <= md.Length; i++)
        {
            var (start, length) = projection.MapSourceSelectionToVisible(i, 0);
            Assert.AreEqual(0, length);
            Assert.IsTrue(start >= last, $"non-monotonic at {i}: {start} < {last}");
            Assert.IsTrue(start <= projection.VisibleText.Length);
            last = start;
        }
    }

    [TestMethod]
    public void Projection_SourceRangeToVisible_CoversBoldWord()
    {
        var md = "one **two** three";
        var projection = MarkdownSelectionProjection.Create(md);
        var (start, length) = projection.MapSourceSelectionToVisible(4, 7); // "**two**"
        Assert.AreEqual("two", projection.VisibleText.Substring(start, length));
        (start, length) = projection.MapSourceSelectionToVisible(6, 3); // "two"
        Assert.AreEqual("two", projection.VisibleText.Substring(start, length));
    }
}
