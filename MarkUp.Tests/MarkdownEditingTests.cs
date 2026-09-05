using Microsoft.VisualStudio.TestTools.UnitTesting;
using MarkUp.Core;

namespace MarkUp.Tests;

[TestClass]
public sealed class MarkdownEditingTests
{
    // ── ContinueListOnEnter ──────────────────────────────────────────────────

    [TestMethod]
    public void ContinueListOnEnter_PlainParagraph_ReturnsNull()
    {
        Assert.IsNull(MarkdownEditing.ContinueListOnEnter("just text", 9));
    }

    [TestMethod]
    public void ContinueListOnEnter_EmptyDocument_ReturnsNull()
    {
        Assert.IsNull(MarkdownEditing.ContinueListOnEnter(string.Empty, 0));
    }

    [TestMethod]
    public void ContinueListOnEnter_BulletItem_InsertsNextBullet()
    {
        var result = MarkdownEditing.ContinueListOnEnter("- item", 6);
        Assert.IsNotNull(result);
        Assert.AreEqual("- item\n- ", result.NewText);
        Assert.AreEqual(9, result.NewSelectionStart);
        Assert.AreEqual(0, result.NewSelectionLength);
    }

    [TestMethod]
    public void ContinueListOnEnter_StarAndPlusBullets_KeepSameGlyph()
    {
        Assert.AreEqual("* a\n* ", MarkdownEditing.ContinueListOnEnter("* a", 3)!.NewText);
        Assert.AreEqual("+ a\n+ ", MarkdownEditing.ContinueListOnEnter("+ a", 3)!.NewText);
    }

    [TestMethod]
    public void ContinueListOnEnter_OrderedItem_IncrementsNumber()
    {
        var result = MarkdownEditing.ContinueListOnEnter("1. first\n2. second", 18);
        Assert.AreEqual("1. first\n2. second\n3. ", result!.NewText);
    }

    [TestMethod]
    public void ContinueListOnEnter_ParenthesisNumbering_KeepsDelimiter()
    {
        var result = MarkdownEditing.ContinueListOnEnter("7) seven", 8);
        Assert.AreEqual("7) seven\n8) ", result!.NewText);
    }

    [TestMethod]
    public void ContinueListOnEnter_TaskItem_InsertsUncheckedTask()
    {
        var result = MarkdownEditing.ContinueListOnEnter("- [x] done", 10);
        Assert.AreEqual("- [x] done\n- [ ] ", result!.NewText);
    }

    [TestMethod]
    public void ContinueListOnEnter_Blockquote_InsertsQuoteMarker()
    {
        var result = MarkdownEditing.ContinueListOnEnter("> quote", 7);
        Assert.AreEqual("> quote\n> ", result!.NewText);
    }

    [TestMethod]
    public void ContinueListOnEnter_NestedItem_PreservesIndentation()
    {
        var result = MarkdownEditing.ContinueListOnEnter("- a\n  - b", 9);
        Assert.AreEqual("- a\n  - b\n  - ", result!.NewText);
    }

    [TestMethod]
    public void ContinueListOnEnter_CaretMidItem_MovesTailToNewItem()
    {
        var result = MarkdownEditing.ContinueListOnEnter("- hello world", 7);
        Assert.AreEqual("- hello\n- world", result!.NewText);
        Assert.AreEqual(10, result.NewSelectionStart);
    }

    [TestMethod]
    public void ContinueListOnEnter_CaretBeforeMarker_ReturnsNull()
    {
        Assert.IsNull(MarkdownEditing.ContinueListOnEnter("- item", 0));
        Assert.IsNull(MarkdownEditing.ContinueListOnEnter("- item", 1));
    }

    [TestMethod]
    public void ContinueListOnEnter_EmptyItem_RemovesMarkerToEndList()
    {
        var result = MarkdownEditing.ContinueListOnEnter("- a\n- ", 6);
        Assert.AreEqual("- a\n", result!.NewText);
        Assert.AreEqual(4, result.NewSelectionStart);
    }

    [TestMethod]
    public void ContinueListOnEnter_EmptyNestedItem_KeepsIndentOnly()
    {
        var result = MarkdownEditing.ContinueListOnEnter("- a\n  - ", 8);
        Assert.AreEqual("- a\n  ", result!.NewText);
        Assert.AreEqual(6, result.NewSelectionStart);
    }

    [TestMethod]
    public void ContinueListOnEnter_EmptyOrderedItem_RemovesMarker()
    {
        var result = MarkdownEditing.ContinueListOnEnter("1. a\n2. ", 8);
        Assert.AreEqual("1. a\n", result!.NewText);
    }

    [TestMethod]
    public void ContinueListOnEnter_CrLfDocument_InsertsLfOnly()
    {
        var result = MarkdownEditing.ContinueListOnEnter("- a\r\n- b", 8);
        Assert.AreEqual("- a\r\n- b\n- ", result!.NewText);
    }

    [TestMethod]
    public void ContinueListOnEnter_NotAtLineEnd_StillContinues()
    {
        // Caret at the end of the first of two items
        var result = MarkdownEditing.ContinueListOnEnter("- a\n- b", 3);
        Assert.AreEqual("- a\n- \n- b", result!.NewText);
    }

    // ── IndentLines / OutdentLines ───────────────────────────────────────────

    [TestMethod]
    public void IndentLines_NoSelectionOnPlainLine_InsertsSpacesAtCaret()
    {
        var result = MarkdownEditing.IndentLines("ab", 1, 0);
        Assert.AreEqual("a  b", result.NewText);
        Assert.AreEqual(3, result.NewSelectionStart);
    }

    [TestMethod]
    public void IndentLines_NoSelectionOnListItem_IndentsWholeLine()
    {
        var result = MarkdownEditing.IndentLines("- item", 6, 0);
        Assert.AreEqual("  - item", result.NewText);
        Assert.AreEqual(8, result.NewSelectionStart);
    }

    [TestMethod]
    public void IndentLines_SelectionSpanningLines_IndentsEachLine()
    {
        var text = "one\ntwo\nthree";
        var result = MarkdownEditing.IndentLines(text, 1, 8); // "ne\ntwo\nt"
        Assert.AreEqual("  one\n  two\n  three", result.NewText);
        Assert.AreEqual(0, result.NewSelectionStart);
        Assert.AreEqual(15, result.NewSelectionLength); // through "  t" of third line
    }

    [TestMethod]
    public void IndentLines_SelectionEndingAtLineStart_DoesNotIndentNextLine()
    {
        var result = MarkdownEditing.IndentLines("one\ntwo", 0, 4); // "one\n"
        Assert.AreEqual("  one\ntwo", result.NewText);
    }

    [TestMethod]
    public void OutdentLines_RemovesUpToTwoSpaces()
    {
        var result = MarkdownEditing.OutdentLines("  - a\n - b\n- c", 0, 14);
        Assert.AreEqual("- a\n- b\n- c", result.NewText);
        Assert.AreEqual(0, result.NewSelectionStart);
        Assert.AreEqual(11, result.NewSelectionLength);
    }

    [TestMethod]
    public void OutdentLines_RemovesLeadingTab()
    {
        var result = MarkdownEditing.OutdentLines("\tx", 2, 0);
        Assert.AreEqual("x", result.NewText);
        Assert.AreEqual(1, result.NewSelectionStart);
    }

    [TestMethod]
    public void OutdentLines_CaretInsideIndent_ClampsToLineStart()
    {
        var result = MarkdownEditing.OutdentLines("  x", 1, 0);
        Assert.AreEqual("x", result.NewText);
        Assert.AreEqual(0, result.NewSelectionStart);
    }

    [TestMethod]
    public void OutdentLines_NothingToRemove_LeavesTextUnchanged()
    {
        var result = MarkdownEditing.OutdentLines("x\ny", 0, 3);
        Assert.AreEqual("x\ny", result.NewText);
        Assert.AreEqual(3, result.NewSelectionLength);
    }

    [TestMethod]
    public void IndentThenOutdent_RoundTrips()
    {
        var text = "- a\n- b";
        var indented = MarkdownEditing.IndentLines(text, 0, text.Length);
        var back = MarkdownEditing.OutdentLines(indented.NewText, indented.NewSelectionStart, indented.NewSelectionLength);
        Assert.AreEqual(text, back.NewText);
    }

    // ── ToggleTaskItem ───────────────────────────────────────────────────────

    [TestMethod]
    public void ToggleTaskItem_FirstItem_ChecksIt()
    {
        var result = MarkdownEditing.ToggleTaskItem("- [ ] a\n- [ ] b", 0);
        Assert.AreEqual("- [x] a\n- [ ] b", result!.NewText);
    }

    [TestMethod]
    public void ToggleTaskItem_SecondItem_UnchecksIt()
    {
        var result = MarkdownEditing.ToggleTaskItem("- [ ] a\n- [X] b", 1);
        Assert.AreEqual("- [ ] a\n- [ ] b", result!.NewText);
    }

    [TestMethod]
    public void ToggleTaskItem_SkipsNonTaskLines()
    {
        var md = "# Title\n\n- plain\n- [ ] task\n\ntext";
        var result = MarkdownEditing.ToggleTaskItem(md, 0);
        Assert.AreEqual("# Title\n\n- plain\n- [x] task\n\ntext", result!.NewText);
    }

    [TestMethod]
    public void ToggleTaskItem_CountsBlockquotedTasksInDocumentOrder()
    {
        var md = "> - [ ] quoted\n\n- [ ] top";
        Assert.AreEqual("> - [x] quoted\n\n- [ ] top", MarkdownEditing.ToggleTaskItem(md, 0)!.NewText);
        Assert.AreEqual("> - [ ] quoted\n\n- [x] top", MarkdownEditing.ToggleTaskItem(md, 1)!.NewText);
    }

    [TestMethod]
    public void ToggleTaskItem_LinkBulletIsNotATask()
    {
        var md = "- [link](http://x)\n- [ ] real";
        Assert.AreEqual("- [link](http://x)\n- [x] real", MarkdownEditing.ToggleTaskItem(md, 0)!.NewText);
    }

    [TestMethod]
    public void ToggleTaskItem_IndexOutOfRange_ReturnsNull()
    {
        Assert.IsNull(MarkdownEditing.ToggleTaskItem("- [ ] a", 1));
        Assert.IsNull(MarkdownEditing.ToggleTaskItem("- [ ] a", -1));
        Assert.IsNull(MarkdownEditing.ToggleTaskItem(string.Empty, 0));
    }

    [TestMethod]
    public void ToggleTaskItem_CrLfDocument_PreservesLineEndings()
    {
        var result = MarkdownEditing.ToggleTaskItem("- [ ] a\r\n- [ ] b", 1);
        Assert.AreEqual("- [ ] a\r\n- [x] b", result!.NewText);
    }

    // ── TextEdit ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void TextEdit_IdenticalTexts_IsEmpty()
    {
        var edit = TextEdit.Compute("abc", "abc");
        Assert.IsTrue(edit.IsEmpty);
        Assert.AreEqual(3, edit.Start);
    }

    [TestMethod]
    public void TextEdit_WrapWithBold_ReplacesOnlyTheSelectedSpan()
    {
        var edit = TextEdit.Compute("say hello there", "say **hello** there");
        Assert.AreEqual(4, edit.Start);
        Assert.AreEqual(5, edit.RemovedLength);
        Assert.AreEqual("**hello**", edit.InsertedText);
    }

    [TestMethod]
    public void TextEdit_PureInsertion_HasZeroRemoved()
    {
        var edit = TextEdit.Compute("ac", "abc");
        Assert.AreEqual(1, edit.Start);
        Assert.AreEqual(0, edit.RemovedLength);
        Assert.AreEqual("b", edit.InsertedText);
    }

    [TestMethod]
    public void TextEdit_PureDeletion_HasEmptyInsert()
    {
        var edit = TextEdit.Compute("abc", "ac");
        Assert.AreEqual(1, edit.Start);
        Assert.AreEqual(1, edit.RemovedLength);
        Assert.AreEqual(string.Empty, edit.InsertedText);
    }

    [TestMethod]
    public void TextEdit_RepeatedCharacters_DoesNotOverlapPrefixAndSuffix()
    {
        // "aa" -> "aaa": prefix consumes both a's, suffix must not double-count them.
        var edit = TextEdit.Compute("aa", "aaa");
        Assert.AreEqual(2, edit.Start);
        Assert.AreEqual(0, edit.RemovedLength);
        Assert.AreEqual("a", edit.InsertedText);
    }

    [TestMethod]
    public void TextEdit_ApplyingEditReproducesNewText()
    {
        var pairs = new[]
        {
            ("", "hello"), ("hello", ""), ("- a\n- b", "- a\n  - b"), ("**x**", "x"),
            ("line1\nline2", "line1\n- \nline2"), ("abcabc", "abcXabc"),
        };
        foreach (var (oldText, newText) in pairs)
        {
            var edit = TextEdit.Compute(oldText, newText);
            var applied = oldText.Remove(edit.Start, edit.RemovedLength).Insert(edit.Start, edit.InsertedText);
            Assert.AreEqual(newText, applied, $"'{oldText}' -> '{newText}'");
        }
    }
}

[TestClass]
public sealed class LineEndingTests
{
    [TestMethod]
    public void ContinueListOnEnter_BareCrDocument_TreatsCrAsLineBreak()
    {
        // WinUI TextBox stores a typed Enter as '\r'
        var result = MarkdownEditing.ContinueListOnEnter("- a\r- b", 7);
        Assert.AreEqual("- a\r- b\n- ", result!.NewText);
    }

    [TestMethod]
    public void IndentLines_CrLfDocument_PreservesBreaks()
    {
        var result = MarkdownEditing.IndentLines("a\r\nb", 0, 4);
        Assert.AreEqual("  a\r\n  b", result.NewText);
    }

    [TestMethod]
    public void ToggleTaskItem_BareCrDocument_FindsSecondItem()
    {
        var result = MarkdownEditing.ToggleTaskItem("- [ ] a\r- [ ] b", 1);
        Assert.AreEqual("- [ ] a\r- [x] b", result!.NewText);
    }

    [TestMethod]
    public void Projection_BareCrDocument_SeparatesBlocks()
    {
        var projection = MarkdownSelectionProjection.Create("# Title\r\rpara");
        Assert.AreEqual("Title\npara", projection.VisibleText);
        var (start, length) = projection.MapSourceSelectionToVisible(9, 4); // "para"
        Assert.AreEqual("para", projection.VisibleText.Substring(start, length));
    }

    [TestMethod]
    public void Projection_CrLfDocument_MatchesLfDocument()
    {
        var crlf = MarkdownSelectionProjection.Create("a **b**\r\n\r\n- c");
        var lf = MarkdownSelectionProjection.Create("a **b**\n\n- c");
        Assert.AreEqual(lf.VisibleText, crlf.VisibleText);
    }

    [TestMethod]
    public void Document_DetectLineEnding_PrefersCrLf()
    {
        Assert.AreEqual("\r\n", MarkdownDocument.DetectLineEnding("a\r\nb\nc"));
        Assert.AreEqual("\n", MarkdownDocument.DetectLineEnding("a\nb"));
        Assert.AreEqual("\r", MarkdownDocument.DetectLineEnding("a\rb"));
        Assert.AreEqual("\r\n", MarkdownDocument.DetectLineEnding("no breaks"));
        Assert.AreEqual("\n", MarkdownDocument.DetectLineEnding("no breaks", "\n"));
    }

    [TestMethod]
    public void Document_GetContentForSave_NormalisesTypedNewlines()
    {
        var doc = new MarkdownDocument { Content = "one\rtwo\r\nthree\nfour" };
        doc.LineEnding = "\n";
        Assert.AreEqual("one\ntwo\nthree\nfour", doc.GetContentForSave());
        doc.LineEnding = "\r\n";
        Assert.AreEqual("one\r\ntwo\r\nthree\r\nfour", doc.GetContentForSave());
    }

    [TestMethod]
    public void Document_Reset_RestoresDefaultLineEnding()
    {
        var doc = new MarkdownDocument { LineEnding = "\n" };
        doc.Reset();
        Assert.AreEqual("\r\n", doc.LineEnding);
    }
}
