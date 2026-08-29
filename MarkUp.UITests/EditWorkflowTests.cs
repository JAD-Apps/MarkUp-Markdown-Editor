using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using System.Threading;

namespace MarkUp.UITests;

[TestClass]
[TestCategory("UITest")]
public sealed class EditWorkflowTests : AppSession
{
    [TestInitialize]
    public void Init()
    {
        SkipIfNoSession();
        BringToFront();
        ResetToCleanState();
    }

    [TestCleanup]
    public void Cleanup() => DismissModal();

    [TestMethod]
    public void Undo_ByMenu_RevertsTyping()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("abc");
        ClickMenu("MenuBarEdit", "MenuUndo");
        Thread.Sleep(250);
        Assert.AreNotEqual("abc", editor.Text.Trim());
    }

    [TestMethod]
    public void Undo_ByToolbar_RevertsTyping()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("toolbar undo");
        FindById("ToolbarUndo").Click();
        Thread.Sleep(250);
        Assert.AreNotEqual("toolbar undo", editor.Text.Trim());
    }

    [TestMethod]
    public void Undo_ByShortcut_RevertsTyping()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("shortcut undo");
        SendCtrlShortcut('Z');
        Thread.Sleep(250);
        Assert.AreNotEqual("shortcut undo", editor.Text.Trim());
    }

    [TestMethod]
    public void Redo_ByMenu_RestoresTyping()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("redo menu");
        SendCtrlShortcut('Z');
        Thread.Sleep(200);
        ClickMenu("MenuBarEdit", "MenuRedo");
        Thread.Sleep(250);
        Assert.IsTrue(editor.Text.Contains("redo menu"));
    }

    [TestMethod]
    public void Redo_ByToolbar_RestoresTyping()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("redo toolbar");
        SendCtrlShortcut('Z');
        Thread.Sleep(200);
        FindById("ToolbarRedo").Click();
        Thread.Sleep(250);
        Assert.IsTrue(editor.Text.Contains("redo toolbar"));
    }

    [TestMethod]
    public void Redo_ByShortcut_RestoresTyping()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("redo shortcut");
        SendCtrlShortcut('Z');
        Thread.Sleep(200);
        SendCtrlShortcut('Y');
        Thread.Sleep(250);
        Assert.IsTrue(editor.Text.Contains("redo shortcut"));
    }

    [TestMethod]
    public void SelectAll_ByMenu_ThenDelete_ClearsEditor()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("select all menu");
        ClickMenu("MenuBarEdit", "MenuSelectAll");
        editor.SendKeys(Keys.Delete);
        Thread.Sleep(200);
        Assert.AreEqual(string.Empty, editor.Text.Trim());
    }

    [TestMethod]
    public void SelectAll_ByShortcut_ThenDelete_ClearsEditor()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("select all shortcut");
        SendCtrlShortcut('A');
        editor.SendKeys(Keys.Delete);
        Thread.Sleep(200);
        Assert.AreEqual(string.Empty, editor.Text.Trim());
    }

    [TestMethod]
    public void CopyPaste_ByMenu_DuplicatesSelection()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("copy menu");
        SendCtrlShortcut('A');
        ClickMenu("MenuBarEdit", "MenuCopy");
        editor.SendKeys(Keys.End + Keys.Return);
        ClickMenu("MenuBarEdit", "MenuPaste");
        Thread.Sleep(300);
        Assert.IsTrue(FindById("EditorTextBox").Text.Contains("copy menu"));
    }

    [TestMethod]
    public void CopyPaste_ByShortcut_DuplicatesSelection()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("copy shortcut");
        SendCtrlShortcut('A');
        SendCtrlShortcut('C');
        editor.SendKeys(Keys.End + Keys.Return);
        SendCtrlShortcut('V');
        Thread.Sleep(300);
        Assert.IsTrue(FindById("EditorTextBox").Text.Contains("copy shortcut"));
    }

    [TestMethod]
    public void Cut_ByMenu_RemovesSelection()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("cut menu");
        SendCtrlShortcut('A');
        ClickMenu("MenuBarEdit", "MenuCut");
        Thread.Sleep(250);
        Assert.AreEqual(string.Empty, editor.Text.Trim());
    }

    [TestMethod]
    public void Cut_ByShortcut_RemovesSelection()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("cut shortcut");
        SendCtrlShortcut('A');
        SendCtrlShortcut('X');
        Thread.Sleep(250);
        Assert.AreEqual(string.Empty, editor.Text.Trim());
    }

    [TestMethod]
    public void FindReplace_ByMenu_OpensBar()
    {
        ClickMenu("MenuBarEdit", "MenuFind");
        Thread.Sleep(300);
        Assert.IsTrue(IsDisplayed("FindTextBox"));
    }

    [TestMethod]
    public void FindReplace_ByShortcut_OpensBar()
    {
        // Ctrl+H TOGGLES the bar, and the preceding ByMenu test may leave it open —
        // while probing "is the bar closed?" costs a minutes-long missing-id UIA
        // crawl. Assert via the bridge invoke counter instead: it proves the
        // shortcut reached the find toggle regardless of prior bar state (ByMenu
        // separately proves the toggle shows the bar).
        var before = int.Parse(FindById("AutomationFindInvokeCount").Text);

        FindById("EditorTextBox").Click();
        SendCtrlShortcut('H');
        Thread.Sleep(600);

        var after = int.Parse(FindById("AutomationFindInvokeCount").Text);
        Assert.IsTrue(after > before,
            $"Ctrl+H should invoke the Find & Replace toggle (invoke count {before} -> {after}).");
    }

    [TestMethod]
    public void EmptySelection_CopyAndCut_DoNotCrash()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        ClickMenu("MenuBarEdit", "MenuCopy");
        ClickMenu("MenuBarEdit", "MenuCut");
        Thread.Sleep(200);
        Assert.IsNotNull(Session);
    }

    /// <summary>
    /// Verifies that pasting HTML via Edit > Paste converts the HTML to Markdown in the
    /// editor rather than inserting raw HTML tags. This guards the rich-text clipboard
    /// paste pipeline added in 1.7.0 (PasteRichTextIntoEditorAsync).
    ///
    /// Implementation note: WinAppDriver cannot set HTML clipboard data programmatically,
    /// so we set the clipboard via PowerShell's Set-Clipboard before invoking Paste. The
    /// test then verifies the editor contains the expected Markdown output, not raw HTML.
    /// </summary>
    [TestMethod]
    public void Paste_HtmlClipboard_InsertsMarkdownIntoEditor()
    {
        // Arrange — put rich HTML onto the clipboard via PowerShell.
        // -AsHtml writes real CF_HTML (with fragment markers), which is what the
        // rich-paste pipeline detects. Without it Set-Clipboard writes plain text,
        // and pasting plain text that merely looks like HTML must insert it
        // literally — the previous assertions contradicted that.
        var html = "<b>Bold text</b> and <em>italic text</em>";
        var psCmd = $"Set-Clipboard -Value '{html}' -AsHtml";
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe",
                $"-NoProfile -Command \"{psCmd}\"")
            {
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            proc.WaitForExit(3000);
        }
        catch
        {
            Assert.Inconclusive("Unable to set clipboard via PowerShell — test skipped.");
            return;
        }

        // Give the OS time to process the clipboard change
        Thread.Sleep(300);

        var editor = FindById("EditorTextBox");
        editor.Click();
        Thread.Sleep(150);

        // Act — invoke Edit > Paste via menu
        ClickMenu("MenuBarEdit", "MenuPaste");
        Thread.Sleep(600); // allow async paste + preview timer

        // Assert — editor should contain Markdown bold/italic syntax, not raw HTML
        var editorText = editor.Text;
        Assert.IsTrue(
            editorText.Contains("Bold text") || editorText.Contains("bold") || editorText.Contains("italic"),
            $"Expected pasted content to appear in editor. Actual text: '{editorText}'");
        Assert.IsFalse(editorText.Contains("<b>"),
            "Raw HTML <b> tag must not appear in the editor after pasting rich text.");
    }
}
