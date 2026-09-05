using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using System.Threading;

namespace MarkUp.UITests;

/// <summary>
/// Keyboard behaviours of the source editor that go beyond a plain TextBox: list
/// continuation on Enter, Tab indentation, undo across formatting commands, and rich-text
/// paste via Ctrl+V. All drive the real control through WinAppDriver.
/// </summary>
[TestClass]
[TestCategory("UITest")]
public sealed class EditorBehaviourTests : AppSession
{
    private static AppiumElement? _editor;
    private static AppiumElement Editor => GetCachedElement(ref _editor, "EditorTextBox");

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        if (!IsSessionAvailable) return;
        _editor = TryFindById("EditorTextBox");
    }

    [TestInitialize]
    public void Init()
    {
        SkipIfNoSession();
        BringToFront();
        ResetToCleanState();
    }

    [TestCleanup]
    public void Cleanup() => DismissModal();

    private static string EditorText() => FindById("EditorTextBox").Text;

    [TestMethod]
    public void Enter_OnBulletItem_ContinuesList()
    {
        Editor.Click();
        Editor.SendKeys("- first");
        SendEnterKey();
        Thread.Sleep(200);
        Editor.SendKeys("second");
        Thread.Sleep(200);

        var text = EditorText();
        Assert.IsTrue(text.Contains("- first"), $"first item missing: '{text}'");
        Assert.IsTrue(text.Contains("- second"), $"Enter should have started a new '- ' item: '{text}'");
    }

    [TestMethod]
    public void Enter_OnNumberedItem_IncrementsNumber()
    {
        Editor.Click();
        Editor.SendKeys("1. one");
        SendEnterKey();
        Thread.Sleep(200);
        Editor.SendKeys("two");
        Thread.Sleep(200);

        Assert.IsTrue(EditorText().Contains("2. two"), $"expected '2. two' in '{EditorText()}'");
    }

    [TestMethod]
    public void Enter_OnEmptyBulletItem_EndsList()
    {
        Editor.Click();
        Editor.SendKeys("- only");
        SendEnterKey();
        Thread.Sleep(200);
        // Now on an empty "- " item: Enter again should remove the marker.
        SendEnterKey();
        Thread.Sleep(200);
        Editor.SendKeys("plain");
        Thread.Sleep(200);

        var text = EditorText();
        Assert.IsTrue(text.Contains("- only"), text);
        Assert.IsFalse(text.Contains("- plain"), $"marker should have been removed: '{text}'");
        Assert.IsTrue(text.Contains("plain"), text);
    }

    [TestMethod]
    public void Enter_OnPlainLine_InsertsPlainNewline()
    {
        Editor.Click();
        Editor.SendKeys("alpha");
        SendEnterKey();
        Thread.Sleep(200);
        Editor.SendKeys("beta");
        Thread.Sleep(200);

        var text = EditorText();
        Assert.IsTrue(text.Contains("alpha") && text.Contains("beta"), text);
        Assert.IsFalse(text.Contains("- "), $"no marker expected: '{text}'");
    }

    [TestMethod]
    public void Tab_OnListItem_IndentsLine()
    {
        Editor.Click();
        Editor.SendKeys("- child");
        Editor.SendKeys(Keys.Tab);
        Thread.Sleep(250);

        var text = EditorText();
        Assert.IsTrue(text.StartsWith("  - child"), $"expected two-space indent: '{text}'");
        // Focus must have stayed in the editor (Tab did not move focus)
        Editor.SendKeys("x");
        Thread.Sleep(150);
        Assert.IsTrue(EditorText().Contains("childx"), $"typing after Tab should land in the editor: '{EditorText()}'");
    }

    [TestMethod]
    public void ShiftTab_OnIndentedLine_Outdents()
    {
        Editor.Click();
        Editor.SendKeys("- child");
        Editor.SendKeys(Keys.Tab);
        Thread.Sleep(200);
        Assert.IsTrue(EditorText().StartsWith("  - child"), EditorText());

        Editor.SendKeys(Keys.Shift + Keys.Tab + Keys.Shift);
        Thread.Sleep(250);
        Assert.IsTrue(EditorText().StartsWith("- child"), $"expected outdent: '{EditorText()}'");
    }

    [TestMethod]
    public void Tab_OnPlainText_InsertsSpaces()
    {
        Editor.Click();
        Editor.SendKeys("ab");
        Editor.SendKeys(Keys.Tab);
        Editor.SendKeys("c");
        Thread.Sleep(250);
        Assert.IsTrue(EditorText().StartsWith("ab  c"), $"expected 'ab  c': '{EditorText()}'");
    }

    [TestMethod]
    public void Undo_AfterBoldShortcut_RestoresOriginalText()
    {
        Editor.Click();
        Editor.SendKeys("undo me");
        Editor.Click();
        Thread.Sleep(100);
        SendCtrlShortcut('A');
        Thread.Sleep(150);
        SendCtrlShortcut('B');
        Thread.Sleep(300);
        Assert.IsTrue(EditorText().Contains("**undo me**"), $"bold not applied: '{EditorText()}'");

        SendCtrlShortcut('Z');
        Thread.Sleep(300);
        var text = EditorText();
        Assert.IsFalse(text.Contains("**"), $"undo should remove the bold markers: '{text}'");
        Assert.IsTrue(text.Contains("undo me"), $"undo must not wipe the typed text: '{text}'");
    }

    [TestMethod]
    public void Undo_AfterListContinuation_RevertsInsertedMarker()
    {
        Editor.Click();
        Editor.SendKeys("- item");
        SendEnterKey();
        Thread.Sleep(200);
        Assert.IsTrue(EditorText().TrimEnd().EndsWith("-"), $"expected trailing marker: '{EditorText()}'");

        SendCtrlShortcut('Z');
        Thread.Sleep(300);
        Assert.AreEqual("- item", EditorText().Trim(), "undo should revert the continuation edit only");
    }

    [TestMethod]
    public void CtrlV_HtmlClipboard_PastesMarkdown()
    {
        var html = "<b>Bold text</b> and <em>italic text</em>";
        var psCmd = $"Set-Clipboard -Value '{html}' -AsHtml";
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", $"-NoProfile -Command \"{psCmd}\"")
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
        Thread.Sleep(300);

        Editor.Click();
        Thread.Sleep(150);
        SendCtrlShortcut('V');
        Thread.Sleep(700);

        var text = EditorText();
        Assert.IsTrue(text.Contains("**Bold text**"), $"expected Markdown bold from Ctrl+V: '{text}'");
        Assert.IsTrue(text.Contains("*italic text*"), $"expected Markdown italic from Ctrl+V: '{text}'");
        Assert.IsFalse(text.Contains("<b>"), $"raw HTML must not be inserted: '{text}'");
    }
}
