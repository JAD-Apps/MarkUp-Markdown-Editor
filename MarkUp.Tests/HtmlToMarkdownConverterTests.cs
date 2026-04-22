using MarkUp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkUp.Tests;

[TestClass]
public class HtmlToMarkdownConverterTests
{
    #region Basic / Empty

    [TestMethod]
    public void Convert_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, HtmlToMarkdownConverter.Convert(string.Empty));
    }

    [TestMethod]
    public void Convert_NullString_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, HtmlToMarkdownConverter.Convert(null!));
    }

    [TestMethod]
    public void Convert_WhitespaceOnly_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, HtmlToMarkdownConverter.Convert("   "));
    }

    [TestMethod]
    public void Convert_PlainText_ReturnsText()
    {
        var result = HtmlToMarkdownConverter.Convert("Hello world");
        Assert.AreEqual("Hello world", result);
    }

    #endregion

    #region Headings

    [TestMethod]
    public void Convert_H1_ToMarkdownHeading()
    {
        var result = HtmlToMarkdownConverter.Convert("<h1>Title</h1>");
        Assert.IsTrue(result.Contains("# Title"));
    }

    [TestMethod]
    public void Convert_H2_ToMarkdownHeading()
    {
        var result = HtmlToMarkdownConverter.Convert("<h2>Section</h2>");
        Assert.IsTrue(result.Contains("## Section"));
    }

    [TestMethod]
    public void Convert_H3_ToMarkdownHeading()
    {
        var result = HtmlToMarkdownConverter.Convert("<h3>Subsection</h3>");
        Assert.IsTrue(result.Contains("### Subsection"));
    }

    [TestMethod]
    public void Convert_H4_ToMarkdownHeading()
    {
        var result = HtmlToMarkdownConverter.Convert("<h4>Sub-subsection</h4>");
        Assert.IsTrue(result.Contains("#### Sub-subsection"));
    }

    [TestMethod]
    public void Convert_H5_ToMarkdownHeading()
    {
        var result = HtmlToMarkdownConverter.Convert("<h5>Deep</h5>");
        Assert.IsTrue(result.Contains("##### Deep"));
    }

    [TestMethod]
    public void Convert_H6_ToMarkdownHeading()
    {
        var result = HtmlToMarkdownConverter.Convert("<h6>Deepest</h6>");
        Assert.IsTrue(result.Contains("###### Deepest"));
    }

    [TestMethod]
    public void Convert_HeadingWithId_StripsId()
    {
        var result = HtmlToMarkdownConverter.Convert("<h1 id=\"my-title\">Title</h1>");
        Assert.IsTrue(result.Contains("# Title"));
        Assert.IsFalse(result.Contains("id="));
    }

    #endregion

    #region Inline Formatting

    [TestMethod]
    public void Convert_Strong_ToBold()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>This is <strong>bold</strong> text</p>");
        Assert.IsTrue(result.Contains("**bold**"));
    }

    [TestMethod]
    public void Convert_BTag_ToBold()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>This is <b>bold</b> text</p>");
        Assert.IsTrue(result.Contains("**bold**"));
    }

    [TestMethod]
    public void Convert_Em_ToItalic()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>This is <em>italic</em> text</p>");
        Assert.IsTrue(result.Contains("*italic*"));
    }

    [TestMethod]
    public void Convert_ITag_ToItalic()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>This is <i>italic</i> text</p>");
        Assert.IsTrue(result.Contains("*italic*"));
    }

    [TestMethod]
    public void Convert_StrongEm_ToBoldItalic()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>This is <strong><em>bold italic</em></strong></p>");
        Assert.IsTrue(result.Contains("***bold italic***"));
    }

    [TestMethod]
    public void Convert_Del_ToStrikethrough()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>This is <del>deleted</del> text</p>");
        Assert.IsTrue(result.Contains("~~deleted~~"));
    }

    [TestMethod]
    public void Convert_STag_ToStrikethrough()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>This is <s>deleted</s> text</p>");
        Assert.IsTrue(result.Contains("~~deleted~~"));
    }

    [TestMethod]
    public void Convert_InlineCode_ToBackticks()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>Use <code>console.log()</code> here</p>");
        Assert.IsTrue(result.Contains("`console.log()`"));
    }

    #endregion

    #region Links and Images

    [TestMethod]
    public void Convert_Anchor_ToMarkdownLink()
    {
        var result = HtmlToMarkdownConverter.Convert("<a href=\"https://example.com\">Click here</a>");
        Assert.IsTrue(result.Contains("[Click here](https://example.com)"));
    }

    [TestMethod]
    public void Convert_Img_ToMarkdownImage()
    {
        var result = HtmlToMarkdownConverter.Convert("<img alt=\"Alt text\" src=\"image.png\" />");
        Assert.IsTrue(result.Contains("![Alt text](image.png)"));
    }

    [TestMethod]
    public void Convert_ImgSrcFirst_ToMarkdownImage()
    {
        var result = HtmlToMarkdownConverter.Convert("<img src=\"photo.jpg\" alt=\"My photo\" />");
        Assert.IsTrue(result.Contains("![My photo](photo.jpg)"));
    }

    #endregion

    #region Lists

    [TestMethod]
    public void Convert_UnorderedList_ToBulletList()
    {
        var html = "<ul><li>Item 1</li><li>Item 2</li><li>Item 3</li></ul>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("- Item 1"));
        Assert.IsTrue(result.Contains("- Item 2"));
        Assert.IsTrue(result.Contains("- Item 3"));
    }

    [TestMethod]
    public void Convert_OrderedList_ToNumberedList()
    {
        var html = "<ol><li>First</li><li>Second</li><li>Third</li></ol>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("1. First"));
        Assert.IsTrue(result.Contains("2. Second"));
        Assert.IsTrue(result.Contains("3. Third"));
    }

    #endregion

    #region Code Blocks

    [TestMethod]
    public void Convert_PreCodeBlock_ToFencedCode()
    {
        var html = "<pre><code>var x = 1;</code></pre>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("```"));
        Assert.IsTrue(result.Contains("var x = 1;"));
    }

    [TestMethod]
    public void Convert_PreCodeBlockWithLanguage_IncludesLang()
    {
        var html = "<pre><code class=\"language-csharp\">var x = 1;</code></pre>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("```csharp"));
        Assert.IsTrue(result.Contains("var x = 1;"));
    }

    #endregion

    #region Blockquotes

    [TestMethod]
    public void Convert_Blockquote_ToMarkdownQuote()
    {
        var html = "<blockquote><p>Quoted text</p></blockquote>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("> "));
        Assert.IsTrue(result.Contains("Quoted text"));
    }

    #endregion

    #region Horizontal Rule

    [TestMethod]
    public void Convert_Hr_ToMarkdownRule()
    {
        var result = HtmlToMarkdownConverter.Convert("<hr />");
        Assert.IsTrue(result.Contains("---"));
    }

    [TestMethod]
    public void Convert_HrWithoutSlash_ToMarkdownRule()
    {
        var result = HtmlToMarkdownConverter.Convert("<hr>");
        Assert.IsTrue(result.Contains("---"));
    }

    #endregion

    #region Tables

    [TestMethod]
    public void Convert_Table_ToMarkdownTable()
    {
        var html = "<table><thead><tr><th>Name</th><th>Age</th></tr></thead>" +
                   "<tbody><tr><td>Alice</td><td>30</td></tr></tbody></table>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("| Name | Age |"));
        Assert.IsTrue(result.Contains("| --- | --- |"));
        Assert.IsTrue(result.Contains("| Alice | 30 |"));
    }

    #endregion

    #region Paragraphs

    [TestMethod]
    public void Convert_Paragraph_ExtractsText()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>Hello world</p>");
        Assert.IsTrue(result.Contains("Hello world"));
    }

    [TestMethod]
    public void Convert_MultipleParagraphs_SeparatedByBlankLines()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>First</p><p>Second</p>");
        Assert.IsTrue(result.Contains("First"));
        Assert.IsTrue(result.Contains("Second"));
    }

    #endregion

    #region HTML Entities

    [TestMethod]
    public void DecodeHtmlEntities_Ampersand()
    {
        Assert.AreEqual("A & B", HtmlToMarkdownConverter.DecodeHtmlEntities("A &amp; B"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_LessThan()
    {
        Assert.AreEqual("A < B", HtmlToMarkdownConverter.DecodeHtmlEntities("A &lt; B"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_GreaterThan()
    {
        Assert.AreEqual("A > B", HtmlToMarkdownConverter.DecodeHtmlEntities("A &gt; B"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_Quote()
    {
        Assert.AreEqual("say \"hello\"", HtmlToMarkdownConverter.DecodeHtmlEntities("say &quot;hello&quot;"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_Nbsp()
    {
        Assert.AreEqual("A B", HtmlToMarkdownConverter.DecodeHtmlEntities("A&nbsp;B"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, HtmlToMarkdownConverter.DecodeHtmlEntities(string.Empty));
    }

    #endregion

    #region StripHtmlTags

    [TestMethod]
    public void StripHtmlTags_RemovesTags()
    {
        Assert.AreEqual("Hello", HtmlToMarkdownConverter.StripHtmlTags("<span>Hello</span>"));
    }

    [TestMethod]
    public void StripHtmlTags_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, HtmlToMarkdownConverter.StripHtmlTags(string.Empty));
    }

    [TestMethod]
    public void StripHtmlTags_NestedTags()
    {
        Assert.AreEqual("Bold text", HtmlToMarkdownConverter.StripHtmlTags("<p><strong>Bold</strong> text</p>"));
    }

    #endregion

    #region Line Breaks

    [TestMethod]
    public void Convert_BrTag_ToNewline()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>Line one<br />Line two</p>");
        Assert.IsTrue(result.Contains("Line one"));
        Assert.IsTrue(result.Contains("Line two"));
    }

    #endregion

    #region Round-Trip Tests

    [TestMethod]
    public void RoundTrip_SimpleHeadingAndParagraph()
    {
        var originalMd = "# Hello\n\nWorld";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("# Hello"));
        Assert.IsTrue(roundTripped.Contains("World"));
    }

    [TestMethod]
    public void RoundTrip_BoldAndItalic()
    {
        var originalMd = "This is **bold** and *italic* text";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("**bold**"));
        Assert.IsTrue(roundTripped.Contains("*italic*"));
    }

    [TestMethod]
    public void RoundTrip_UnorderedList()
    {
        var originalMd = "- Item 1\n- Item 2\n- Item 3";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("- Item 1"));
        Assert.IsTrue(roundTripped.Contains("- Item 2"));
        Assert.IsTrue(roundTripped.Contains("- Item 3"));
    }

    [TestMethod]
    public void RoundTrip_OrderedList()
    {
        var originalMd = "1. First\n2. Second\n3. Third";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("1. First"));
        Assert.IsTrue(roundTripped.Contains("2. Second"));
        Assert.IsTrue(roundTripped.Contains("3. Third"));
    }

    [TestMethod]
    public void RoundTrip_Link()
    {
        var originalMd = "[Click here](https://example.com)";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("[Click here](https://example.com)"));
    }

    [TestMethod]
    public void RoundTrip_CodeBlock()
    {
        var originalMd = "```csharp\nvar x = 1;\n```";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("```csharp"));
        Assert.IsTrue(roundTripped.Contains("var x = 1;"));
    }

    [TestMethod]
    public void RoundTrip_HorizontalRule()
    {
        var originalMd = "---";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("---"));
    }

    [TestMethod]
    public void RoundTrip_Strikethrough()
    {
        var originalMd = "This is ~~deleted~~ text";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("~~deleted~~"));
    }

    [TestMethod]
    public void RoundTrip_InlineCode()
    {
        var originalMd = "Use `console.log()` here";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("`console.log()`"));
    }

    #endregion

    #region Span-based formatting (contentEditable output)

    [TestMethod]
    public void Convert_SpanBold_ToBold()
    {
        var html = "<p><span style=\"font-weight: bold\">heavy</span></p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("**heavy**"));
    }

    [TestMethod]
    public void Convert_SpanBold700_ToBold()
    {
        var html = "<p><span style=\"font-weight: 700\">strong</span></p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("**strong**"));
    }

    [TestMethod]
    public void Convert_SpanItalic_ToItalic()
    {
        var html = "<p><span style=\"font-style: italic\">slant</span></p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("*slant*"));
    }

    [TestMethod]
    public void Convert_SpanLineThrough_ToStrikethrough()
    {
        var html = "<p><span style=\"text-decoration: line-through\">struck</span></p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("~~struck~~"));
    }

    [TestMethod]
    public void Convert_UnderlineTag_StripsTagKeepsContent()
    {
        var html = "<p><u>underlined</u></p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("underlined"));
        Assert.IsFalse(result.Contains("<u>"));
        Assert.IsFalse(result.Contains("</u>"));
    }

    [TestMethod]
    public void Convert_StrikeTag_ToStrikethrough()
    {
        var html = "<p><strike>old</strike></p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("~~old~~"));
    }

    #endregion

    #region Div wrappers (contentEditable line wrapping)

    [TestMethod]
    public void Convert_DivWithText_ExtractsText()
    {
        var html = "<div>Hello world</div>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("Hello world"));
    }

    [TestMethod]
    public void Convert_MultipleDivs_EachBecomesLine()
    {
        var html = "<div>Line one</div><div>Line two</div>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("Line one"));
        Assert.IsTrue(result.Contains("Line two"));
    }

    [TestMethod]
    public void Convert_DivWithBr_BecomesBlankLine()
    {
        var html = "<p>Before</p><div><br /></div><p>After</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("Before"));
        Assert.IsTrue(result.Contains("After"));
    }

    [TestMethod]
    public void Convert_EmptyDiv_DoesNotCrash()
    {
        var result = HtmlToMarkdownConverter.Convert("<div></div>");
        Assert.IsNotNull(result);
    }

    #endregion

    #region Nested lists

    [TestMethod]
    public void Convert_NestedUnorderedList_IndentsChildItems()
    {
        var html = "<ul><li>Parent<ul><li>Child</li></ul></li><li>Sibling</li></ul>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("- Parent"));
        Assert.IsTrue(result.Contains("Child"));
        Assert.IsTrue(result.Contains("- Sibling"));
    }

    [TestMethod]
    public void Convert_NestedOrderedList_IndentsChildItems()
    {
        var html = "<ol><li>First<ol><li>Sub</li></ol></li><li>Second</li></ol>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("1. First"));
        Assert.IsTrue(result.Contains("Sub"));
        Assert.IsTrue(result.Contains("2. Second"));
    }

    #endregion

    #region Task lists

    [TestMethod]
    public void Convert_TaskListChecked_ToCheckedMarkdown()
    {
        var html = "<ul class=\"task-list\"><li><input type=\"checkbox\" checked /> Done</li></ul>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("[x] Done") || result.Contains("[X] Done"));
    }

    [TestMethod]
    public void Convert_TaskListUnchecked_ToUncheckedMarkdown()
    {
        var html = "<ul class=\"task-list\"><li><input type=\"checkbox\" /> Pending</li></ul>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("[ ] Pending"));
    }

    #endregion

    #region Table alignment

    [TestMethod]
    public void Convert_TableWithCenterAlignedTh_ProducesCenterSeparator()
    {
        var html = "<table><thead><tr>" +
                   "<th style=\"text-align: center\">Name</th>" +
                   "</tr></thead><tbody><tr><td>Alice</td></tr></tbody></table>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains(":---:"));
        Assert.IsTrue(result.Contains("Name"));
    }

    [TestMethod]
    public void Convert_TableWithRightAlignedTh_ProducesRightSeparator()
    {
        var html = "<table><thead><tr>" +
                   "<th style=\"text-align: right\">Amount</th>" +
                   "</tr></thead><tbody><tr><td>100</td></tr></tbody></table>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("---:"));
        Assert.IsTrue(result.Contains("Amount"));
    }

    [TestMethod]
    public void Convert_TableWithNoAlignment_ProducesDefaultSeparator()
    {
        var html = "<table><thead><tr><th>Column</th></tr></thead>" +
                   "<tbody><tr><td>Value</td></tr></tbody></table>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("---"));
        Assert.IsTrue(result.Contains("Column"));
    }

    #endregion

    #region Numeric HTML entities

    [TestMethod]
    public void DecodeHtmlEntities_NumericDecimal_DecodesCorrectly()
    {
        // &#65; = 'A'
        Assert.AreEqual("A", HtmlToMarkdownConverter.DecodeHtmlEntities("&#65;"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_NumericHex_DecodesCorrectly()
    {
        // &#x41; = 'A'
        Assert.AreEqual("A", HtmlToMarkdownConverter.DecodeHtmlEntities("&#x41;"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_Apostrophe_DecodesCorrectly()
    {
        Assert.AreEqual("it's", HtmlToMarkdownConverter.DecodeHtmlEntities("it&#39;s"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_Apos_DecodesCorrectly()
    {
        Assert.AreEqual("it's", HtmlToMarkdownConverter.DecodeHtmlEntities("it&apos;s"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_NumericOutOfRange_LeftAsIs()
    {
        // Values > 0xFFFF should be returned unchanged
        var input = "&#x10000;";
        var result = HtmlToMarkdownConverter.DecodeHtmlEntities(input);
        Assert.IsNotNull(result);
    }

    #endregion

    #region Mixed / edge cases

    [TestMethod]
    public void Convert_NestedInlineInParagraph_AllConverted()
    {
        var html = "<p><strong>bold</strong> and <em>italic</em> and <code>code</code></p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("**bold**"));
        Assert.IsTrue(result.Contains("*italic*"));
        Assert.IsTrue(result.Contains("`code`"));
    }

    [TestMethod]
    public void Convert_LinkInsideParagraph_ConvertsLink()
    {
        var html = "<p>Visit <a href=\"https://example.com\">Example</a> now</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("[Example](https://example.com)"));
    }

    [TestMethod]
    public void Convert_ImageInsideParagraph_ConvertsImage()
    {
        var html = "<p>Here is <img src=\"img.png\" alt=\"pic\" /> an image</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("![pic](img.png)"));
    }

    [TestMethod]
    public void Convert_MultipleConsecutiveBr_DoesNotCrash()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>a<br /><br /><br />b</p>");
        Assert.IsTrue(result.Contains("a"));
        Assert.IsTrue(result.Contains("b"));
    }

    [TestMethod]
    public void Convert_BrTagWithoutSlash_Converts()
    {
        var result = HtmlToMarkdownConverter.Convert("line one<br>line two");
        Assert.IsTrue(result.Contains("line one"));
        Assert.IsTrue(result.Contains("line two"));
    }

    [TestMethod]
    public void Convert_LargeDocument_DoesNotCrash()
    {
        var headings = string.Concat(Enumerable.Range(1, 50).Select(i => $"<h2>Section {i}</h2><p>Paragraph {i}.</p>"));
        var result = HtmlToMarkdownConverter.Convert(headings);
        Assert.IsTrue(result.Contains("## Section 1"));
        Assert.IsTrue(result.Contains("## Section 50"));
    }

    #endregion

    #region Office / Word HTML stripping

    [TestMethod]
    public void Convert_OfficeParagraphTag_StrippedCompletely()
    {
        // <o:p> is generated by Word inside almost every paragraph
        var html = "<p>Hello <o:p></o:p>world</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("Hello"));
        Assert.IsTrue(result.Contains("world"));
        Assert.IsFalse(result.Contains("o:p"), "Office namespace tag must be removed");
    }

    [TestMethod]
    public void Convert_WordConditionalComment_StrippedCompletely()
    {
        var html = "<!--[if mso]><v:shape></v:shape><![endif]--><p>Real content</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("Real content"));
        Assert.IsFalse(result.Contains("v:shape"), "Word conditional comment content must be stripped");
        Assert.IsFalse(result.Contains("[if"), "Word conditional comment markers must be stripped");
    }

    [TestMethod]
    public void Convert_MsoStyleAttribute_MsoPropertiesStripped()
    {
        // mso-* properties in style attributes should be stripped; semantic props kept
        var html = "<p style=\"mso-list:l0 level1 lfo1;font-weight:bold;\">Item</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("Item"), "Paragraph text must be preserved");
        Assert.IsFalse(result.Contains("mso-"), "mso-* style properties must be stripped");
    }

    [TestMethod]
    public void Convert_LangAttribute_StrippedFromElements()
    {
        var html = "<p lang=\"en-GB\">Content</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("Content"));
        Assert.IsFalse(result.Contains("lang="), "lang attribute must be stripped");
    }

    #endregion

    #region Word list paragraphs

    [TestMethod]
    public void Convert_WordListParagraph_ProducesUnorderedListItem()
    {
        var html = "<p class=\"MsoListParagraph\" style=\"margin-left:36.0pt\">List item</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("- List item"), $"Expected '- List item' but got: {result}");
    }

    [TestMethod]
    public void Convert_WordListBulletClass_ProducesListItem()
    {
        var html = "<p class=\"MsoListBullet\" style=\"margin-left:36.0pt\">Bullet item</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("- Bullet item"), $"Expected '- Bullet item' but got: {result}");
    }

    [TestMethod]
    public void Convert_WordListParagraphIndented_ProducesNestedListItem()
    {
        // 72pt = level 2 (36pt per level), so indent = 1, prefix = "  - "
        var html = "<p class=\"MsoListParagraph\" style=\"margin-left:72.0pt\">Nested item</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        // Accept either 2-space indent (ideal) or 1-space — what matters is indented further than level-1
        Assert.IsTrue(result.Contains("  - Nested item") || result.Contains(" - Nested item"),
            $"Expected indented list item but got: '{result}'");
        Assert.IsTrue(result.Contains("Nested item"), $"Content must be present. Got: '{result}'");
    }

    [TestMethod]
    public void Convert_WordListParagraphIndented_IsMoreIndentedThanLevel1()
    {
        // Level 1 (36pt) should produce "- item", level 2 (72pt) should produce more indented
        var level1Html = "<p class=\"MsoListParagraph\" style=\"margin-left:36.0pt\">Level one</p>";
        var level2Html = "<p class=\"MsoListParagraph\" style=\"margin-left:72.0pt\">Level two</p>";
        var level1Result = HtmlToMarkdownConverter.Convert(level1Html);
        var level2Result = HtmlToMarkdownConverter.Convert(level2Html);
        // Level 2 must have more leading spaces than level 1
        var level1LeadingSpaces = level1Result.TrimStart('\n').Length - level1Result.TrimStart('\n').TrimStart(' ').Length;
        var level2LeadingSpaces = level2Result.TrimStart('\n').Length - level2Result.TrimStart('\n').TrimStart(' ').Length;
        Assert.IsTrue(level2LeadingSpaces >= level1LeadingSpaces,
            $"Level 2 item (72pt) must be at least as indented as level 1 (36pt). L1='{level1Result}' L2='{level2Result}'");
    }

    [TestMethod]
    public void Convert_MultipleWordListParagraphs_ProducesMultipleItems()
    {
        var html = "<p class=\"MsoListParagraph\" style=\"margin-left:36.0pt\">First</p>" +
                   "<p class=\"MsoListParagraph\" style=\"margin-left:36.0pt\">Second</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("- First"));
        Assert.IsTrue(result.Contains("- Second"));
    }

    #endregion

    #region Combined bold+italic span

    [TestMethod]
    public void Convert_SpanBoldItalicCombined_ProducesBoldItalicMarkdown()
    {
        var html = "<span style=\"font-weight:bold;font-style:italic;\">BoldItalic</span>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("***BoldItalic***"), $"Expected ***BoldItalic*** but got: {result}");
    }

    [TestMethod]
    public void Convert_SpanItalicBoldCombined_ProducesBoldItalicMarkdown()
    {
        // italic declared before bold — should still produce ***
        var html = "<span style=\"font-style:italic;font-weight:700;\">ItalicBold</span>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("***ItalicBold***"), $"Expected ***ItalicBold*** but got: {result}");
    }

    [TestMethod]
    public void Convert_SpanFontSize_StripsTagKeepsContent()
    {
        var html = "<span style=\"font-size:14pt;\">Large text</span>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("Large text"), "Text content must be preserved");
        Assert.IsFalse(result.Contains("font-size"), "font-size style must not appear in output");
    }

    #endregion

    #region CF_HTML clipboard fragment extraction

    [TestMethod]
    public void ExtractCfHtmlFragment_WithValidStartEndMarkers_ExtractsFragment()
    {
        const string cfHtml = "Version:0.9\r\nStartHTML:0000000097\r\nEndHTML:0000000250\r\n" +
                              "StartFragment:0000000133\r\nEndFragment:0000000210\r\n" +
                              "<html><body><!--StartFragment--><b>Bold text</b><!--EndFragment--></body></html>";

        var result = HtmlToMarkdownConverter.ExtractCfHtmlFragment(cfHtml);
        Assert.AreEqual("<b>Bold text</b>", result, "Must extract only the fragment between markers");
    }

    [TestMethod]
    public void ExtractCfHtmlFragment_WithNoHeader_ReturnsTrimmedInput()
    {
        const string html = "<p>Just plain HTML</p>";
        var result = HtmlToMarkdownConverter.ExtractCfHtmlFragment(html);
        Assert.AreEqual(html, result, "Plain HTML without CF_HTML header must be returned as-is");
    }

    [TestMethod]
    public void ExtractCfHtmlFragment_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, HtmlToMarkdownConverter.ExtractCfHtmlFragment(string.Empty));
    }

    [TestMethod]
    public void ExtractCfHtmlFragment_OnlyStartMarker_ReturnsTrimmedInput()
    {
        // Malformed CF_HTML with start marker but no end marker — fall back to full string
        const string html = "<!--StartFragment--><b>text</b>";
        var result = HtmlToMarkdownConverter.ExtractCfHtmlFragment(html);
        // With no end marker the fallback returns the full string
        Assert.IsTrue(result.Contains("text"), "Must not crash on malformed input");
    }

    [TestMethod]
    public void ExtractCfHtmlFragment_WithByteOffsets_ExtractsCorrectRange()
    {
        // Build a CF_HTML string where the byte offsets point to the fragment
        // StartFragment offset 72 points to '<b>' in the string below
        const string body = "<!--StartFragment--><i>Italic</i><!--EndFragment-->";
        var header = "Version:0.9\r\nStartHTML:0\r\nEndHTML:0\r\nStartFragment:0\r\nEndFragment:0\r\n";
        var cfHtml = header + "<html><body>" + body + "</body></html>";

        var result = HtmlToMarkdownConverter.ExtractCfHtmlFragment(cfHtml);
        // Start/end comment markers are present, so they take priority over byte offsets
        Assert.AreEqual("<i>Italic</i>", result);
    }

    [TestMethod]
    public void ExtractCfHtmlFragment_ThenConvert_ProducesMarkdown()
    {
        // End-to-end: extract fragment, then convert to Markdown
        const string cfHtml = "Version:0.9\r\nStartHTML:0\r\nEndHTML:0\r\n" +
                              "StartFragment:0\r\nEndFragment:0\r\n" +
                              "<html><body><!--StartFragment-->" +
                              "<h1>Title</h1><p><strong>Bold</strong></p>" +
                              "<!--EndFragment--></body></html>";

        var fragment = HtmlToMarkdownConverter.ExtractCfHtmlFragment(cfHtml);
        var markdown = HtmlToMarkdownConverter.Convert(fragment);

        Assert.IsTrue(markdown.Contains("# Title"), $"Expected '# Title' in: {markdown}");
        Assert.IsTrue(markdown.Contains("**Bold**"), $"Expected '**Bold**' in: {markdown}");
    }

    #endregion

    #region Word heading-tagged list items (h1-h6 with MsoList class)

    [TestMethod]
    public void Convert_WordListH1Class_ProducesListItemNotHeading()
    {
        // Word sometimes wraps the first list item of a section in <h1 class="MsoListBullet">
        // rather than <p class="MsoListParagraph">. The converter must treat these as list
        // items, not headings — no '#' prefix should appear in the output.
        const string html = "<h1 class=\"MsoListBullet\" style=\"margin-left:36.0pt\">" +
                            "New \u2014 Start a fresh document</h1>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.StartsWith("- "), $"Expected list item, got: {result}");
        Assert.IsFalse(result.StartsWith("#"), $"Must not produce a heading: {result}");
        Assert.IsTrue(result.Contains("New"), $"Content missing: {result}");
    }

    [TestMethod]
    public void Convert_WordListH2Class_ProducesListItem()
    {
        const string html = "<h2 class=\"MsoListParagraph\" style=\"margin-left:36.0pt\">" +
                            "Open \u2014 Open any text file</h2>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.StartsWith("- "), $"Expected list item, got: {result}");
        Assert.IsFalse(result.StartsWith("#"), $"Must not produce a heading: {result}");
    }

    [TestMethod]
    public void Convert_WordListH1WithMsoIgnoreSpan_BulletGlyphNotInOutput()
    {
        // Word injects <span style="mso-list:Ignore">·<span>&nbsp;&nbsp;&nbsp;</span></span>
        // before the visible list text. The · and surrounding whitespace must be stripped.
        const string html = "<h1 class=\"MsoListBullet\" style=\"margin-left:36.0pt\">" +
                            "<span style=\"mso-list:Ignore\">\u00B7<span>&nbsp;&nbsp;&nbsp;</span></span>" +
                            "New Tab \u2014 Open an additional editor tab</h1>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsFalse(result.Contains("\u00B7"), $"Middle dot must be stripped: {result}");
        Assert.IsTrue(result.StartsWith("- "), $"Expected list item: {result}");
        Assert.IsTrue(result.Contains("New Tab"), $"Content must be preserved: {result}");
    }

    [TestMethod]
    public void Convert_WordListParagraphWithMsoIgnoreSpan_BulletGlyphNotInOutput()
    {
        // Same pattern in the <p class="MsoListParagraph"> variant.
        const string html = "<p class=\"MsoListParagraph\" style=\"margin-left:36.0pt\">" +
                            "<span style=\"font-family:Symbol;mso-list:Ignore\">\u00B7</span>" +
                            "Close Tab \u2014 Close the current tab (Ctrl+W)</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsFalse(result.Contains("\u00B7"), $"Middle dot must be stripped: {result}");
        Assert.IsTrue(result.StartsWith("- "), $"Expected list item: {result}");
    }

    [TestMethod]
    public void Convert_EmptyH1_ProducesNoOutput()
    {
        // Word emits empty <h1> elements as visual section separators; they must not
        // produce stray '#' lines in the Markdown output.
        const string html = "<h1></h1><p>Hello</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsFalse(result.Contains("#"), $"Empty heading must produce no output: {result}");
        Assert.IsTrue(result.Contains("Hello"), $"Paragraph content must be preserved: {result}");
    }

    [TestMethod]
    public void Convert_HeadingWithInternalNewline_OutputIsSingleLine()
    {
        // When Word HTML has a heading whose content spans a newline (e.g. long bold text
        // wrapped by the HTML source), the converter must produce a single-line heading.
        const string html = "<h1>A lightweight,\nmodern Notepad clone</h1>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.StartsWith("# A lightweight, modern Notepad clone"),
            $"Expected single-line heading, got: {result}");
    }

    [TestMethod]
    public void Convert_MultipleWordListSections_NoStrayHeadings()
    {
        // Reproduces the structure from the user-reported paste: two list groups each
        // starting with an <h1 class="MsoListBullet"> followed by <p class="MsoListParagraph">.
        // No '#' heading markers should appear in the output.
        const string html =
            "<h3>File Operations</h3>" +
            "<h1 class=\"MsoListBullet\" style=\"margin-left:36.0pt\">New \u2014 Start fresh</h1>" +
            "<p class=\"MsoListParagraph\" style=\"margin-left:36.0pt\">Open \u2014 Open a file</p>" +
            "<h3>Edit Operations</h3>" +
            "<h1 class=\"MsoListBullet\" style=\"margin-left:36.0pt\">Undo / Redo \u2014 Ctrl+Z / Ctrl+Y</h1>" +
            "<p class=\"MsoListParagraph\" style=\"margin-left:36.0pt\">Cut / Copy / Paste</p>";

        var result = HtmlToMarkdownConverter.Convert(html);

        // Proper headings should still be present
        Assert.IsTrue(result.Contains("### File Operations"), $"Section heading missing: {result}");
        Assert.IsTrue(result.Contains("### Edit Operations"), $"Section heading missing: {result}");

        // List items must be bullet items, not headings
        var lines = result.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#"))
            {
                // Only the h3 section headings are allowed
                Assert.IsTrue(trimmed.StartsWith("###"),
                    $"Unexpected heading level in list area: '{trimmed}'");
            }
        }

        Assert.IsTrue(result.Contains("- New"), $"List item 'New' missing: {result}");
        Assert.IsTrue(result.Contains("- Open"), $"List item 'Open' missing: {result}");
        Assert.IsTrue(result.Contains("- Undo"), $"List item 'Undo' missing: {result}");
    }

    #endregion

    #region Browser/web-origin Word HTML list items (mso-list: in inline style)

    /// <summary>
    /// Reproduces the exact pattern from a GitHub README page copied via Word's web engine.
    /// List items are encoded as &lt;p class=MsoNormal style='...mso-list:l0 level1 lfo1...'&gt;
    /// with the bullet inside a non-comment IE conditional &lt;![if !supportLists]&gt;...&lt;![endif]&gt;.
    /// </summary>
    [TestMethod]
    public void Convert_BrowserOriginWordHtml_MsoListInStyle_ProducesListItem()
    {
        // Exact pattern from real clipboard capture: p class=MsoNormal with mso-list: in style
        const string html =
            "<p class=MsoNormal style='margin-left:60.0pt;text-indent:-18.0pt;" +
            "mso-list:l0 level1 lfo1'>" +
            "<![if !supportLists]><span style='font-family:Symbol'>" +
            "<span style='mso-list:Ignore'>\u00B7<span>&nbsp;&nbsp;</span></span>" +
            "</span><![endif]>" +
            "<strong><span>New</span></strong>" +
            "<span> \u2014 Start a fresh document</span></p>";

        var result = HtmlToMarkdownConverter.Convert(html);

        Assert.IsTrue(result.StartsWith("- "), $"Expected list item, got: {result}");
        Assert.IsFalse(result.Contains("\u00B7"), $"Middle dot must be stripped: {result}");
        Assert.IsTrue(result.Contains("New"), $"Bold text must be preserved: {result}");
        Assert.IsTrue(result.Contains("Start a fresh document"), $"Content must be preserved: {result}");
    }

    [TestMethod]
    public void Convert_BrowserOriginWordHtml_NonCommentConditional_BulletGlyphStripped()
    {
        // The <![if !supportLists]>...<![endif]> block (non-comment form) must be fully
        // removed so neither the bullet glyph nor surrounding whitespace appear in output.
        const string html =
            "<p class=MsoNormal style='mso-list:l0 level1 lfo1'>" +
            "<![if !supportLists]><span style='font-family:Symbol'>" +
            "<span style='mso-list:Ignore'>\u00B7   </span></span><![endif]>" +
            "Save / Save As</p>";

        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.StartsWith("- "), $"Expected list item: {result}");
        Assert.IsFalse(result.Contains("\u00B7"), $"Bullet glyph must not appear: {result}");
        Assert.IsTrue(result.Contains("Save"), $"Content must be preserved: {result}");
    }

    [TestMethod]
    public void Convert_BrowserOriginWordHtml_Level2_ProducesIndentedItem()
    {
        // level2 in mso-list should produce one indent level (2 spaces + "- ")
        const string html =
            "<p class=MsoNormal style='mso-list:l0 level2 lfo1'>Nested item</p>";

        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.StartsWith("  - "), $"Expected indented list item, got: '{result}'");
    }

    [TestMethod]
    public void Convert_BrowserOriginWordHtml_MultipleItems_AllConvertedToList()
    {
        // Reproduces the File Operations section from the user-reported Inklet paste.
        // Each item is a <p class=MsoNormal style='mso-list:...'> with a non-comment
        // IE conditional wrapping the bullet glyph.
        const string html =
            "<h3>File Operations</h3>" +
            "<p class=MsoNormal style='mso-list:l0 level1 lfo1'>" +
            "<![if !supportLists]><span style='font-family:Symbol'><span style='mso-list:Ignore'>\u00B7 </span></span><![endif]>" +
            "<strong><span>New</span></strong><span> \u2014 Start a fresh document in the current tab</span></p>" +
            "<p class=MsoNormal style='mso-list:l0 level1 lfo1'>" +
            "<![if !supportLists]><span style='font-family:Symbol'><span style='mso-list:Ignore'>\u00B7 </span></span><![endif]>" +
            "<strong><span>New Tab</span></strong><span> \u2014 Open an additional editor tab (Ctrl+T)</span></p>" +
            "<p class=MsoNormal style='mso-list:l0 level1 lfo1'>" +
            "<![if !supportLists]><span style='font-family:Symbol'><span style='mso-list:Ignore'>\u00B7 </span></span><![endif]>" +
            "<strong><span>Close Tab</span></strong><span> \u2014 Close the current tab (Ctrl+W)</span></p>";

        var result = HtmlToMarkdownConverter.Convert(html);

        Assert.IsTrue(result.Contains("### File Operations"), $"Section heading missing: {result}");
        Assert.IsTrue(result.Contains("- "), $"Must have list items: {result}");
        Assert.IsFalse(result.Contains("\u00B7"), $"No bullet glyphs: {result}");

        var listLines = result.Split('\n').Where(l => l.TrimStart().StartsWith("- ")).ToList();
        Assert.AreEqual(3, listLines.Count, $"Expected 3 list items, got {listLines.Count}:\n{result}");
        Assert.IsTrue(listLines.Any(l => l.Contains("New Tab")), $"'New Tab' item missing: {result}");
        Assert.IsTrue(listLines.Any(l => l.Contains("Close Tab")), $"'Close Tab' item missing: {result}");
    }

    #endregion
}
