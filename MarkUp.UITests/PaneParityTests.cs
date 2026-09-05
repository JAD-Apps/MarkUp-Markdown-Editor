using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using System.Diagnostics;
using System.Threading;

namespace MarkUp.UITests;

/// <summary>
/// End-to-end editor⇄preview parity on a multi-block document. The bridge selects arbitrary
/// ranges in either pane and reports what the other pane actually highlighted/selected, so
/// these tests fail on any offset drift between the source projection and the rendered DOM
/// (the class of bug that previously put the mirrored caret one block off).
/// </summary>
[TestClass]
[TestCategory("UITest")]
public sealed class PaneParityTests : AppSession
{
    // Multi-block document: heading, paragraph, bullet list, task list, table.
    private const string Doc =
        "# Title\n\nAlpha **beta** gamma\n\n- one\n- two\n\n- [ ] task a\n- [x] task b\n\n| h1 | h2 |\n|---|---|\n| c1 | c2 |";

    [TestInitialize]
    public void Init()
    {
        SkipIfNoSession();
        BringToFront();
        ResetToCleanState();
    }

    [TestCleanup]
    public void Cleanup() => DismissModal();

    private static void SeedDocument()
    {
        // Appium's keyboard simulation occasionally drops a shifted character (e.g. one '*'
        // of "**"); verify the seed landed intact and retry rather than fail on input noise.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            PasteText(Doc);
            Thread.Sleep(300);
            var doc = DocumentText();
            if (doc.Contains("**beta**") && doc.Contains("- [x] task b") && doc.Contains("| c1 | c2 |"))
                break;
            ResetToCleanState();
        }
        // Let the debounced preview render land before probing it.
        Thread.Sleep(700);
    }

    private static void SetParam(string value)
    {
        var input = FindById("AutomationParamInput");
        input.Click();
        input.SendKeys(value);
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3000)
        {
            try { if (input.Text == value) break; } catch { break; }
            Thread.Sleep(50);
        }
    }

    private static string WaitForBridgeText(string automationId, Func<string, bool> predicate, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        string last = string.Empty;
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            last = TryFindById(automationId)?.Text ?? string.Empty;
            if (predicate(last)) return last;
            Thread.Sleep(100);
        }
        return last;
    }

    private static string DocumentText() => FindById("AutomationDocumentContent").Text;

    private static void SelectEditorRange(int start, int length)
    {
        SetParam($"{start},{length}");
        InvokeBridgeButton(FindById("AutomationEditorSelectRangeButton"));
        Thread.Sleep(400);
    }

    private static void SelectPreviewText(string text)
    {
        SetParam(text);
        InvokeBridgeButton(FindById("AutomationPreviewSelectTextButton"));
        Thread.Sleep(500);
    }

    /// <summary>Editor source offset of <paramref name="needle"/> in the document as the control stores it.</summary>
    private static int SourceIndexOf(string needle)
    {
        var text = DocumentText();
        var idx = text.IndexOf(needle, StringComparison.Ordinal);
        Assert.IsTrue(idx >= 0, $"'{needle}' not found in editor document: '{text}'");
        return idx;
    }

    [TestMethod]
    public void EditorSelection_InParagraphAfterHeading_HighlightsSameTextInPreview()
    {
        SeedDocument();
        var start = SourceIndexOf("gamma");
        SelectEditorRange(start, 5);

        var mirrored = WaitForBridgeText("AutomationPreviewMirroredText", t => t == "gamma");
        Assert.AreEqual("gamma", mirrored, "preview highlight should cover exactly the editor selection");
    }

    [TestMethod]
    public void EditorSelection_InsideBoldRun_HighlightsVisibleWordOnly()
    {
        SeedDocument();
        var start = SourceIndexOf("**beta**");
        SelectEditorRange(start, 8); // include the delimiters

        var mirrored = WaitForBridgeText("AutomationPreviewMirroredText", t => t == "beta");
        Assert.AreEqual("beta", mirrored);
    }

    [TestMethod]
    public void EditorSelection_InSecondListItem_HighlightsThatItem()
    {
        SeedDocument();
        var start = SourceIndexOf("two");
        SelectEditorRange(start, 3);

        var mirrored = WaitForBridgeText("AutomationPreviewMirroredText", t => t == "two");
        Assert.AreEqual("two", mirrored, "list items after other blocks must not drift");
    }

    [TestMethod]
    public void EditorSelection_InTaskItem_HighlightsItemText()
    {
        SeedDocument();
        var start = SourceIndexOf("task b");
        SelectEditorRange(start, 6);

        var mirrored = WaitForBridgeText("AutomationPreviewMirroredText", t => t == "task b");
        Assert.AreEqual("task b", mirrored, "task-list items (checkbox + text) must map exactly");
    }

    [TestMethod]
    public void EditorSelection_InTableCell_HighlightsCell()
    {
        SeedDocument();
        var start = SourceIndexOf("c2");
        SelectEditorRange(start, 2);

        var mirrored = WaitForBridgeText("AutomationPreviewMirroredText", t => t == "c2");
        Assert.AreEqual("c2", mirrored, "table body cells must map exactly");
    }

    [TestMethod]
    public void EditorSelection_SpanningTwoBlocks_HighlightsAcrossBoundary()
    {
        SeedDocument();
        var start = SourceIndexOf("one");
        var end = SourceIndexOf("two") + 3;
        SelectEditorRange(start, end - start);

        var mirrored = WaitForBridgeText("AutomationPreviewMirroredText", t => t.Contains("one") && t.Contains("two"));
        Assert.IsTrue(mirrored.StartsWith("one") && mirrored.EndsWith("two"), $"got '{mirrored}'");
    }

    [TestMethod]
    public void PreviewSelection_InSecondParagraphBlock_SelectsSameTextInEditor()
    {
        SeedDocument();
        SelectPreviewText("gamma");

        var expectedStart = SourceIndexOf("gamma");
        var start = WaitForBridgeText("AutomationEditorSelectionStart", t => t == expectedStart.ToString());
        var length = FindById("AutomationEditorSelectionLength").Text;
        Assert.AreEqual(expectedStart.ToString(), start, "editor selection start must match the preview selection");
        Assert.AreEqual("5", length);
    }

    [TestMethod]
    public void PreviewSelection_OfWholeBoldWord_SelectsIncludingDelimiters()
    {
        SeedDocument();
        SelectPreviewText("beta");

        var expectedStart = SourceIndexOf("**beta**");
        var start = WaitForBridgeText("AutomationEditorSelectionStart", t => t == expectedStart.ToString());
        Assert.AreEqual(expectedStart.ToString(), start);
        Assert.AreEqual("8", FindById("AutomationEditorSelectionLength").Text, "full-token selection includes ** delimiters");
    }

    [TestMethod]
    public void PreviewSelection_InTableCell_SelectsCellSource()
    {
        SeedDocument();
        SelectPreviewText("c1");

        var expectedStart = SourceIndexOf("c1");
        var start = WaitForBridgeText("AutomationEditorSelectionStart", t => t == expectedStart.ToString());
        Assert.AreEqual(expectedStart.ToString(), start);
        Assert.AreEqual("2", FindById("AutomationEditorSelectionLength").Text);
    }

    [TestMethod]
    public void TaskCheckbox_ClickInPreview_TogglesSourceMarker()
    {
        SeedDocument();
        Assert.IsTrue(DocumentText().Contains("- [ ] task a"), DocumentText());

        InvokeBridgeButton(FindById("AutomationPreviewToggleTaskButton"));

        var doc = WaitForBridgeText("AutomationDocumentContent", t => t.Contains("- [x] task a"));
        Assert.IsTrue(doc.Contains("- [x] task a"), $"source should now be checked: '{doc}'");
        Assert.IsTrue(doc.Contains("- [x] task b"), "other items must be untouched");
        Assert.IsTrue(doc.Contains("gamma"), "the rest of the document must survive a checkbox click");

        // And back again.
        InvokeBridgeButton(FindById("AutomationPreviewToggleTaskButton"));
        doc = WaitForBridgeText("AutomationDocumentContent", t => t.Contains("- [ ] task a"));
        Assert.IsTrue(doc.Contains("- [ ] task a"), $"second click should uncheck: '{doc}'");
    }
}
