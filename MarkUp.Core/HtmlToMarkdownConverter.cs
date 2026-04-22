using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkUp.Core;

/// <summary>
/// Converts HTML content back to Markdown.
/// Used by the WYSIWYG preview editor to sync changes back to the Markdown source.
/// </summary>
public static partial class HtmlToMarkdownConverter
{
    /// <summary>
    /// Converts an HTML string (from contentEditable) back to Markdown text.
    /// </summary>
    public static string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // Normalize line endings and whitespace
        var text = html.Replace("\r\n", "\n").Replace("\r", "\n");

        // Convert Word list paragraphs BEFORE stripping Office noise so that margin-left
        // values (used for indent-level detection) are still present in the style attribute.
        text = ConvertWordListParagraphs(text);

        // Strip Office/Word XML namespace noise before any structural conversion
        text = StripOfficeNamespaceTags(text);

        // Process block-level elements first (order matters)
        text = ConvertCodeBlocks(text);
        text = ConvertHeadings(text);
        text = ConvertBlockquotes(text);
        text = ConvertTaskLists(text);
        text = ConvertUnorderedLists(text);
        text = ConvertOrderedLists(text);
        text = ConvertTables(text);
        text = ConvertHorizontalRules(text);
        text = ConvertParagraphs(text);
        text = ConvertDivs(text); // Handle contentEditable div line wrappers

        // Process inline elements
        text = ConvertInlineElements(text);

        // Convert line breaks
        text = LineBreakRegex().Replace(text, "\n");

        // Strip any remaining HTML tags
        text = StripHtmlTags(text);

        // Decode HTML entities
        text = DecodeHtmlEntities(text);

        // Clean up excessive blank lines
        text = ExcessiveNewlinesRegex().Replace(text, "\n\n");

        // Trim leading/trailing newlines only — not spaces, so that indented list items
        // that appear first or last in a conversion result keep their indentation.
        return text.Trim('\n', '\r');
    }

    /// <summary>
    /// Extracts the clean HTML fragment from a CF_HTML clipboard format string.
    /// The CF_HTML format is used by Windows clipboard (WinRT GetHtmlFormatAsync) and
    /// prefixes the actual HTML with a byte-offset header block. This method strips that
    /// header and returns only the content between the StartFragment/EndFragment markers,
    /// matching the approach used by ReverseMarkdown and Turndown-based integrations.
    /// Falls back to returning the full input string if no markers are present.
    /// </summary>
    public static string ExtractCfHtmlFragment(string cfHtml)
    {
        if (string.IsNullOrWhiteSpace(cfHtml))
            return string.Empty;

        // Try to find the explicit HTML comment markers first (most reliable)
        const string startMarker = "<!--StartFragment-->";
        const string endMarker = "<!--EndFragment-->";

        var startIdx = cfHtml.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        var endIdx = cfHtml.IndexOf(endMarker, StringComparison.OrdinalIgnoreCase);

        if (startIdx >= 0 && endIdx > startIdx)
        {
            var fragmentStart = startIdx + startMarker.Length;
            return cfHtml[fragmentStart..endIdx].Trim();
        }

        // Fall back: try byte-offset header fields (StartFragment: NNN / EndFragment: NNN)
        var startFieldMatch = CfHtmlStartOffsetRegex().Match(cfHtml);
        var endFieldMatch = CfHtmlEndOffsetRegex().Match(cfHtml);

        if (startFieldMatch.Success && endFieldMatch.Success
            && int.TryParse(startFieldMatch.Groups[1].Value, out var startOffset)
            && int.TryParse(endFieldMatch.Groups[1].Value, out var endOffset)
            && startOffset >= 0 && endOffset > startOffset && endOffset <= cfHtml.Length)
        {
            return cfHtml[startOffset..endOffset].Trim();
        }

        // No CF_HTML header found — return as-is (already plain HTML)
        return cfHtml.Trim();
    }

    #region Block Elements

    /// <summary>
    /// Strips Office XML namespace tags and Word-specific markup that pollute HTML copied
    /// from Microsoft Word or Outlook. Handles namespace-prefixed elements (o:p, w:*, m:*, v:*),
    /// Word conditional comments, and mso-* style attributes. This replicates the pre-processing
    /// step used by ReverseMarkdown and other HTML→Markdown converters for clipboard content.
    /// </summary>
    private static string StripOfficeNamespaceTags(string html)
    {
        // Strip Word conditional comments (comment form): <!--[if ...]>...</[endif]-->
        html = WordConditionalCommentRegex().Replace(html, string.Empty);

        // Strip Word non-comment IE conditionals (non-comment form): <![if ...]>...</![endif]>
        // These appear around bullet indicator spans in browser-copied Word/GitHub HTML.
        html = WordNonCommentConditionalRegex().Replace(html, string.Empty);

        // Strip Office XML namespace elements and their content: <o:p>, <w:*>, <m:*>, <v:*>
        // These are always paired or self-closing in Word HTML.
        html = OfficeNamespaceElementRegex().Replace(html, string.Empty);

        // Strip mso-* style attributes (double-quoted style="...").
        html = MsoStyleAttributeRegex().Replace(html, m =>
        {
            var styleValue = m.Groups[1].Value;
            var cleaned = MsoPropertyRegex().Replace(styleValue, string.Empty).Trim().TrimEnd(';').Trim();
            return cleaned.Length > 0 ? $"style=\"{cleaned}\"" : string.Empty;
        });

        // Strip mso-* style attributes (single-quoted style='...').
        html = MsoStyleAttributeSingleQuoteRegex().Replace(html, m =>
        {
            var styleValue = m.Groups[1].Value;
            var cleaned = MsoPropertyRegex().Replace(styleValue, string.Empty).Trim().TrimEnd(';').Trim();
            return cleaned.Length > 0 ? $"style='{cleaned}'" : string.Empty;
        });

        // Strip Word-specific class attributes (MsoNormal, MsoListParagraph etc.).
        // Only strip the lang attribute which is pure noise.
        html = WordLangAttributeRegex().Replace(html, string.Empty);

        return html;
    }

    /// <summary>
    /// Converts Word-style list paragraph elements to Markdown unordered list items.
    /// Word uses &lt;p class="MsoListParagraph"&gt; for most list items but also
    /// &lt;h1 class="MsoListBullet"&gt; (or h2-h6) for the first item of each group.
    /// Two regexes handle the two forms; both delegate to the same conversion helper.
    ///
    /// Before stripping tags the inner HTML is cleaned of:
    ///  • mso-list:Ignore spans — the hidden bullet indicator Word inserts
    ///  • Symbol/Wingdings font spans — carry the visible bullet glyph (· etc.)
    /// After stripping tags the resulting text is cleaned of any residual leading
    /// bullet glyphs and internal whitespace runs are collapsed.
    /// Indentation level is inferred from the margin-left CSS value (36pt ≈ level 1).
    /// </summary>
    private static string ConvertWordListParagraphs(string html)
    {
        // Strip non-comment IE conditionals first so bullet indicator spans are visible
        // to the subsequent bullet-stripping logic inside WordListItemReplacement.
        html = WordNonCommentConditionalRegex().Replace(html, string.Empty);

        // <p class="MsoListParagraph"> / <p class="MsoListBullet"> (Mso list class form)
        html = WordListParagraphPRegex().Replace(html, m => WordListItemReplacement(m, contentGroup: 1));
        // <h1 class="MsoListBullet"> etc. (heading-tagged first items in each group)
        html = WordListParagraphHxRegex().Replace(html, m => WordListItemReplacement(m, contentGroup: 1));
        // <p class=MsoNormal style='...mso-list:l0 level1...'> (browser/web-origin Word HTML)
        html = WordMsoListInlineStylePRegex().Replace(html, m => WordListItemReplacement(m, contentGroup: 1));
        return html;
    }

    private static string WordListItemReplacement(Match m, int contentGroup)
    {
        var innerHtml = m.Groups[contentGroup].Value;

        // Strip the hidden mso-list:Ignore bullet indicator span (and its content)
        innerHtml = MsoListIgnoreSpanRegex().Replace(innerHtml, string.Empty);
        // Strip Symbol/Wingdings font spans (carry the visual bullet glyph)
        innerHtml = WordBulletFontSpanRegex().Replace(innerHtml, string.Empty);

        // Extract text, collapse internal whitespace runs (Word HTML has \t and multiple spaces)
        var content = StripHtmlTags(innerHtml);
        content = System.Text.RegularExpressions.Regex.Replace(content, @"[ \t]+", " ");
        content = System.Text.RegularExpressions.Regex.Replace(content, @"\s*\n\s*", " ");
        // Remove any leading bullet/arrow glyph that survived (·, •, –, etc.)
        content = LeadingBulletGlyphRegex().Replace(content, string.Empty);
        content = content.Trim();

        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        // Determine nesting level — prefer mso-list level\d (reliable across sources);
        // fall back to margin-left pt calculation for older Word HTML.
        var indent = 0;
        var levelMatch = MsoListLevelRegex().Match(m.Value);
        if (levelMatch.Success && int.TryParse(levelMatch.Groups[1].Value, out var level))
        {
            indent = Math.Max(0, level - 1);
        }
        else
        {
            var marginMatch = MarginLeftPtRegex().Match(m.Value);
            if (marginMatch.Success && double.TryParse(marginMatch.Groups[1].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var pts))
            {
                indent = Math.Max(0, (int)Math.Round(pts / 36.0) - 1);
            }
        }

        var indentStr = new string(' ', indent * 2) + "- ";
        return $"\n{indentStr}{content}";
    }

    private static string ConvertCodeBlocks(string html)
    {
        // <pre><code class="language-xxx">content</code></pre>
        html = PreCodeWithLangRegex().Replace(html, m =>
        {
            var lang = m.Groups[1].Value;
            var code = DecodeHtmlEntities(StripHtmlTags(m.Groups[2].Value)).Trim();
            return $"\n\n```{lang}\n{code}\n```\n\n";
        });

        // <pre><code>content</code></pre>
        html = PreCodeRegex().Replace(html, m =>
        {
            var code = DecodeHtmlEntities(StripHtmlTags(m.Groups[1].Value)).Trim();
            return $"\n\n```\n{code}\n```\n\n";
        });

        return html;
    }

    private static string ConvertHeadings(string html)
    {
        for (int level = 6; level >= 1; level--)
        {
            var pattern = new Regex($@"<h{level}[^>]*>(.*?)</h{level}>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = pattern.Replace(html, m =>
            {
                var content = StripHtmlTags(m.Groups[1].Value);
                // Collapse any internal whitespace runs (including newlines from Word HTML)
                content = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ").Trim();
                // Skip empty headings — Word emits empty <h1> as section separators
                if (string.IsNullOrWhiteSpace(content))
                    return string.Empty;
                var prefix = new string('#', level);
                return $"\n\n{prefix} {content}\n\n";
            });
        }
        return html;
    }

    private static string ConvertBlockquotes(string html)
    {
        return BlockquoteRegex().Replace(html, m =>
        {
            var innerHtml = m.Groups[1].Value;
            // Recursively convert inner content
            var innerText = StripHtmlTags(innerHtml).Trim();
            var lines = innerText.Split('\n');
            var sb = new StringBuilder();
            sb.AppendLine();
            foreach (var line in lines)
            {
                sb.AppendLine($"> {line.Trim()}");
            }
            sb.AppendLine();
            return sb.ToString();
        });
    }

    private static string ConvertTaskLists(string html)
    {
        return TaskListRegex().Replace(html, m =>
        {
            var innerHtml = m.Groups[1].Value;
            var sb = new StringBuilder();
            sb.AppendLine();

            var items = TaskListItemRegex().Matches(innerHtml);
            foreach (Match item in items)
            {
                var isChecked = item.Value.Contains("checked", StringComparison.OrdinalIgnoreCase);
                var text = StripHtmlTags(item.Groups[1].Value).Trim();
                // Remove leading checkbox-related text
                text = TaskCheckboxTextCleanup().Replace(text, "").Trim();
                var marker = isChecked ? "[x]" : "[ ]";
                sb.AppendLine($"- {marker} {text}");
            }

            sb.AppendLine();
            return sb.ToString();
        });
    }

    private static string ConvertUnorderedLists(string html)
    {
        // Process innermost <ul> blocks first so nesting is handled inside-out
        string prev;
        do
        {
            prev = html;
            html = InnerUlRegex().Replace(html, m =>
            {
                if (m.Value.Contains("task-list", StringComparison.OrdinalIgnoreCase))
                    return m.Value;
                var innerHtml = m.Groups[1].Value;
                var sb = new StringBuilder();
                sb.AppendLine();

                var items = ListItemRegex().Matches(innerHtml);
                foreach (Match item in items)
                {
                    var content = StripHtmlTags(item.Groups[1].Value).Trim();
                    var lines = content.Split('\n');
                    sb.AppendLine($"- {lines[0].Trim()}");
                    // Indent continuation lines (already-converted nested items)
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var line = lines[i].Trim();
                        if (!string.IsNullOrWhiteSpace(line))
                            sb.AppendLine($"  {line}");
                    }
                }

                sb.AppendLine();
                return sb.ToString();
            });
        } while (html != prev);

        return html;
    }

    private static string ConvertOrderedLists(string html)
    {
        // Process innermost <ol> blocks first so nesting is handled inside-out
        string prev;
        do
        {
            prev = html;
            html = InnerOlRegex().Replace(html, m =>
            {
                var innerHtml = m.Groups[1].Value;
                var sb = new StringBuilder();
                sb.AppendLine();

                var items = ListItemRegex().Matches(innerHtml);
                int num = 1;
                foreach (Match item in items)
                {
                    var content = StripHtmlTags(item.Groups[1].Value).Trim();
                    var lines = content.Split('\n');
                    sb.AppendLine($"{num}. {lines[0].Trim()}");
                    // Indent continuation lines (already-converted nested items)
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var line = lines[i].Trim();
                        if (!string.IsNullOrWhiteSpace(line))
                            sb.AppendLine($"   {line}");
                    }
                    num++;
                }

                sb.AppendLine();
                return sb.ToString();
            });
        } while (html != prev);

        return html;
    }

    private static string ConvertTables(string html)
    {
        return TableRegex().Replace(html, m =>
        {
            var tableHtml = m.Value;
            var sb = new StringBuilder();
            sb.AppendLine();

            // Extract header rows
            var theadMatch = TheadRegex().Match(tableHtml);
            if (theadMatch.Success)
            {
                var thCells = ThCellRegex().Matches(theadMatch.Value);
                if (thCells.Count > 0)
                {
                    var headers = new List<string>();
                    var separators = new List<string>();
                    foreach (Match cell in thCells)
                    {
                        headers.Add(StripHtmlTags(cell.Groups[2].Value).Trim());
                        separators.Add(GetTableSeparator(cell.Groups[1].Value));
                    }
                    sb.AppendLine("| " + string.Join(" | ", headers) + " |");
                    sb.AppendLine("| " + string.Join(" | ", separators) + " |");
                }
                else
                {
                    // Fallback: use generic th/td cell regex
                    var headerCells = CellRegex().Matches(theadMatch.Value);
                    var headers = new List<string>();
                    foreach (Match cell in headerCells)
                        headers.Add(StripHtmlTags(cell.Groups[1].Value).Trim());
                    sb.AppendLine("| " + string.Join(" | ", headers) + " |");
                    sb.AppendLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");
                }
            }

            // Extract body rows
            var tbodyMatch = TbodyRegex().Match(tableHtml);
            var bodyHtml = tbodyMatch.Success ? tbodyMatch.Value : tableHtml;
            var rows = TrRegex().Matches(bodyHtml);
            bool isFirst = theadMatch.Success; // skip first row if it was in thead
            foreach (Match row in rows)
            {
                if (!isFirst && !theadMatch.Success)
                {
                    // First row when no thead, use as header
                    var firstCells = CellRegex().Matches(row.Value);
                    var firstHeaders = new List<string>();
                    foreach (Match cell in firstCells)
                    {
                        firstHeaders.Add(StripHtmlTags(cell.Groups[1].Value).Trim());
                    }
                    sb.AppendLine("| " + string.Join(" | ", firstHeaders) + " |");
                    sb.AppendLine("| " + string.Join(" | ", firstHeaders.Select(_ => "---")) + " |");
                    isFirst = true;
                    continue;
                }

                // Only process rows from tbody
                if (theadMatch.Success && !tbodyMatch.Success)
                    continue; // Skip thead rows when iterating all <tr>

                var cells = TdRegex().Matches(row.Value);
                if (cells.Count > 0)
                {
                    var values = new List<string>();
                    foreach (Match cell in cells)
                    {
                        values.Add(StripHtmlTags(cell.Groups[1].Value).Trim());
                    }
                    sb.AppendLine("| " + string.Join(" | ", values) + " |");
                }
            }

            sb.AppendLine();
            return sb.ToString();
        });
    }

    private static string ConvertHorizontalRules(string html)
    {
        return HrRegex().Replace(html, "\n\n---\n\n");
    }

    private static string ConvertParagraphs(string html)
    {
        return ParagraphRegex().Replace(html, m =>
        {
            var content = m.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;
            return $"\n\n{content}\n\n";
        });
    }

    #endregion

    #region Inline Elements

    private static string ConvertInlineElements(string html)
    {
        // Resolve combined bold+italic span before individual bold/italic spans
        html = SpanBoldItalicRegex().Replace(html, "***$1***");
        html = SpanItalicBoldRegex().Replace(html, "***$1***");

        // Resolve span-based formatting emitted by some browsers/editors
        html = SpanBoldRegex().Replace(html, "<strong>$1</strong>");
        html = SpanItalicRegex().Replace(html, "<em>$1</em>");
        html = SpanStrikeRegex().Replace(html, "<del>$1</del>");
        // Font-size span has no Markdown equivalent — strip the tag, keep content
        html = SpanFontSizeRegex().Replace(html, "$1");
        // Underline has no Markdown equivalent — strip the tag, keep content
        html = UnderlineTagRegex().Replace(html, "$1");

        // Images: <img src="url" alt="text" />
        html = ImgRegex().Replace(html, m =>
        {
            var alt = m.Groups[1].Value;
            var src = m.Groups[2].Value;
            return $"![{alt}]({src})";
        });

        // Also handle src before alt
        html = ImgSrcFirstRegex().Replace(html, m =>
        {
            var src = m.Groups[1].Value;
            var alt = m.Groups[2].Value;
            return $"![{alt}]({src})";
        });

        // Links: <a href="url">text</a>
        html = AnchorRegex().Replace(html, m =>
        {
            var href = m.Groups[1].Value;
            var text = StripHtmlTags(m.Groups[2].Value);
            return $"[{text}]({href})";
        });

        // Bold + Italic: <strong><em>text</em></strong>
        html = StrongEmRegex().Replace(html, "***$1***");

        // Bold: <strong>text</strong> or <b>text</b>
        html = StrongRegex().Replace(html, "**$1**");
        html = BTagRegex().Replace(html, "**$1**");

        // Italic: <em>text</em> or <i>text</i>
        html = EmRegex().Replace(html, "*$1*");
        html = ITagRegex().Replace(html, "*$1*");

        // Strikethrough: <del>text</del>, <s>text</s>, or <strike>text</strike>
        html = DelRegex().Replace(html, "~~$1~~");
        html = STagRegex().Replace(html, "~~$1~~");
        html = StrikeTagRegex().Replace(html, "~~$1~~");

        // Inline code: <code>text</code> (not inside <pre>)
        html = InlineCodeRegex().Replace(html, "`$1`");

        return html;
    }

    #endregion

    #region Utility

    // Converts <div> wrappers that contentEditable uses for new lines
    private static string ConvertDivs(string html)
    {
        // <div><br></div> is an empty line
        html = DivBrRegex().Replace(html, "\n\n");
        // <div>content</div> is a line of text
        html = DivContentRegex().Replace(html, m =>
        {
            var content = m.Groups[1].Value.Trim();
            return string.IsNullOrWhiteSpace(content) ? "\n" : $"{content}\n";
        });
        return html;
    }

    private static string GetTableSeparator(string cellAttributes)
    {
        var m = TextAlignRegex().Match(cellAttributes);
        if (!m.Success) return "---";
        return m.Groups[1].Value.Trim().ToLowerInvariant() switch
        {
            "center" => ":---:",
            "right" => "---:",
            _ => "---"
        };
    }

    internal static string StripHtmlTags(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;
        return HtmlTagRegex().Replace(html, string.Empty);
    }

    internal static string DecodeHtmlEntities(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        text = text.Replace("&amp;", "&");
        text = text.Replace("&lt;", "<");
        text = text.Replace("&gt;", ">");
        text = text.Replace("&quot;", "\"");
        text = text.Replace("&#39;", "'");
        text = text.Replace("&apos;", "'");
        text = text.Replace("&nbsp;", " ");

        // Numeric decimal entities: &#160; etc.
        text = NumericEntityRegex().Replace(text, m =>
        {
            if (int.TryParse(m.Groups[1].Value, out int code) && code is >= 0 and <= 0xFFFF)
                return ((char)code).ToString();
            return m.Value;
        });

        // Numeric hex entities: &#xA0; etc.
        text = HexEntityRegex().Replace(text, m =>
        {
            if (int.TryParse(m.Groups[1].Value, NumberStyles.HexNumber, null, out int code) && code is >= 0 and <= 0xFFFF)
                return ((char)code).ToString();
            return m.Value;
        });

        return text;
    }

    #endregion

    #region Regex Patterns

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveNewlinesRegex();

    [GeneratedRegex(@"<pre><code\s+class=""language-([^""]+)"">(.*?)</code></pre>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PreCodeWithLangRegex();

    [GeneratedRegex(@"<pre><code>(.*?)</code></pre>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PreCodeRegex();

    [GeneratedRegex(@"<blockquote>(.*?)</blockquote>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BlockquoteRegex();

    [GeneratedRegex(@"<ul[^>]*class=""task-list""[^>]*>(.*?)</ul>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TaskListRegex();

    [GeneratedRegex(@"<li>(.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TaskListItemRegex();

    [GeneratedRegex(@"^\s*", RegexOptions.None)]
    private static partial Regex TaskCheckboxTextCleanup();

    [GeneratedRegex(@"<ul(?![^>]*task-list)[^>]*>(.*?)</ul>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex UlRegex();

    [GeneratedRegex(@"<ol[^>]*>(.*?)</ol>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex OlRegex();

    [GeneratedRegex(@"<li[^>]*>(.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"<table[^>]*>.*?</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TableRegex();

    [GeneratedRegex(@"<thead[^>]*>.*?</thead>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TheadRegex();

    [GeneratedRegex(@"<tbody[^>]*>.*?</tbody>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TbodyRegex();

    [GeneratedRegex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TrRegex();

    [GeneratedRegex(@"<t[hd][^>]*>(.*?)</t[hd]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CellRegex();

    [GeneratedRegex(@"<td[^>]*>(.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TdRegex();

    [GeneratedRegex(@"<hr\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex HrRegex();

    [GeneratedRegex(@"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ParagraphRegex();

    [GeneratedRegex(@"<img[^>]*alt=""([^""]*)""[^>]*src=""([^""]*)""[^>]*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex ImgRegex();

    [GeneratedRegex(@"<img[^>]*src=""([^""]*)""[^>]*alt=""([^""]*)""[^>]*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex ImgSrcFirstRegex();

    [GeneratedRegex(@"<a[^>]*href=""([^""]*)""[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AnchorRegex();

    [GeneratedRegex(@"<strong><em>(.*?)</em></strong>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StrongEmRegex();

    [GeneratedRegex(@"<strong>(.*?)</strong>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StrongRegex();

    [GeneratedRegex(@"<b>(.*?)</b>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BTagRegex();

    [GeneratedRegex(@"<em>(.*?)</em>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex EmRegex();

    [GeneratedRegex(@"<i>(.*?)</i>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ITagRegex();

    [GeneratedRegex(@"<del>(.*?)</del>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DelRegex();

    [GeneratedRegex(@"<s>(.*?)</s>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex STagRegex();

    [GeneratedRegex(@"<code>(.*?)</code>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    // Innermost list matching (no nested list tags inside) — used for inside-out processing
    [GeneratedRegex(@"<ul(?![^>]*task-list)[^>]*>((?:(?!</?ul\b).)*?)</ul>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InnerUlRegex();

    [GeneratedRegex(@"<ol[^>]*>((?:(?!</?ol\b).)*?)</ol>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InnerOlRegex();

    // <div> handling for contentEditable line wrappers
    [GeneratedRegex(@"<div[^>]*>\s*<br\s*/?>\s*</div>", RegexOptions.IgnoreCase)]
    private static partial Regex DivBrRegex();

    [GeneratedRegex(@"<div[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DivContentRegex();

    // Span-based formatting from browsers/editors
    [GeneratedRegex(@"<span[^>]*style=""[^""]*font-weight\s*:\s*(?:bold|700)[^""]*""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SpanBoldRegex();

    [GeneratedRegex(@"<span[^>]*style=""[^""]*font-style\s*:\s*italic[^""]*""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SpanItalicRegex();

    [GeneratedRegex(@"<span[^>]*style=""[^""]*text-decoration\s*:\s*line-through[^""]*""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SpanStrikeRegex();

    [GeneratedRegex(@"<u>(.*?)</u>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex UnderlineTagRegex();

    [GeneratedRegex(@"<strike>(.*?)</strike>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StrikeTagRegex();

    // Table header cell with attributes (for alignment detection).
    // \b prevents matching <thead> which also starts with <th.
    [GeneratedRegex(@"<th\b([^>]*)>(.*?)</th>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ThCellRegex();

    [GeneratedRegex(@"text-align\s*:\s*(left|right|center)", RegexOptions.IgnoreCase)]
    private static partial Regex TextAlignRegex();

    // Numeric HTML entities
    [GeneratedRegex(@"&#(\d+);")]
    private static partial Regex NumericEntityRegex();

    [GeneratedRegex(@"&#x([0-9a-fA-F]+);", RegexOptions.IgnoreCase)]
    private static partial Regex HexEntityRegex();

    // ── Office / Word HTML stripping ─────────────────────────────────────────

    // Word conditional comments (comment form): <!--[if ...]>...<![endif]-->
    [GeneratedRegex(@"<!--\[if[^\]]*\]>.*?<!\[endif\]-->", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex WordConditionalCommentRegex();

    // Word non-comment IE conditionals: <![if ...]>...</![endif]>
    // Used around bullet indicator spans in browser/web-origin Word HTML.
    [GeneratedRegex(@"<!\[if[^\]]*\]>.*?<!\[endif\]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex WordNonCommentConditionalRegex();

    // Office XML namespace elements: <o:p>, <w:anything>, <m:anything>, <v:anything>
    // Matches both paired (<o:p>...</o:p>) and self-closing (<o:p/>) forms.
    [GeneratedRegex(@"<(?:o|w|m|v):[^>]*>.*?</(?:o|w|m|v):[^>]*>|<(?:o|w|m|v):[^/][^>]*/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex OfficeNamespaceElementRegex();

    // style="..." attributes containing mso-* properties — captured for partial cleanup
    [GeneratedRegex(@"style=""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex MsoStyleAttributeRegex();

    // style='...' single-quoted variant (browser/web-origin Word HTML)
    [GeneratedRegex(@"style='([^']*)'"  , RegexOptions.IgnoreCase)]
    private static partial Regex MsoStyleAttributeSingleQuoteRegex();

    // Individual mso-* CSS properties within a style value (e.g. mso-list:l0 level1 lfo1;)
    [GeneratedRegex(@"mso-[^;""]+;?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex MsoPropertyRegex();

    // lang="..." attribute generated by Word on almost every element
    [GeneratedRegex(@"\s+lang=""[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex WordLangAttributeRegex();

    // Word list paragraphs using a <p> block tag with an Mso list/body class.
    [GeneratedRegex(@"<p\b(?=[^>]*class=""Mso(?:List|Body)[^""]*"")[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex WordListParagraphPRegex();

    // Word list paragraphs using an <h1>–<h6> block tag with an Mso list/body class.
    // Word generates heading-tagged first items for each list group rather than <p>.
    [GeneratedRegex(@"<h[1-6]\b(?=[^>]*class=""Mso(?:List|Body)[^""]*"")[^>]*>(.*?)</h[1-6]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex WordListParagraphHxRegex();

    // <p> whose inline style contains mso-list: — browser/web-origin Word HTML list items.
    // These use class=MsoNormal but mark list membership via mso-list: in the style attribute.
    [GeneratedRegex(@"<p\b(?=[^>]*mso-list\s*:)[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex WordMsoListInlineStylePRegex();

    // mso-list level number: "mso-list:l0 level2 lfo1" → group 1 = "2"
    [GeneratedRegex(@"mso-list\s*:\s*\S+\s+level(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex MsoListLevelRegex();

    // <span style="mso-list:Ignore"> — the hidden bullet indicator span Word inserts
    // These wrap the visible bullet glyph (e.g. · from Symbol font) and must be removed.
    // Handles both double-quoted and single-quoted style attributes.
    [GeneratedRegex(@"<span[^>]*style=[""'][^""']*mso-list\s*:\s*Ignore[^""']*[""'][^>]*>.*?</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MsoListIgnoreSpanRegex();

    // <span style="font-family:Symbol"> or font-family:Wingdings — Word bullet font spans
    // Handles both double-quoted and single-quoted style attributes.
    [GeneratedRegex(@"<span[^>]*style=[""'][^""']*font-family\s*:\s*(?:Symbol|Wingdings)[^""']*[""'][^>]*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex WordBulletFontSpanRegex();

    // Leading bullet/arrow glyphs that Word or Unicode uses as list markers (·, •, ▪, ◦, ─, –)
    [GeneratedRegex(@"^[\u00B7\u2022\u25AA\u25E6\u2013\u2014\u2212\-]\s*")]
    private static partial Regex LeadingBulletGlyphRegex();

    // margin-left value in points inside a style attribute (e.g. margin-left:36.0pt)
    [GeneratedRegex(@"margin-left\s*:\s*([\d.]+)pt", RegexOptions.IgnoreCase)]
    private static partial Regex MarginLeftPtRegex();

    // ── CF_HTML clipboard format ──────────────────────────────────────────────

    // StartFragment byte offset from the CF_HTML header (e.g. "StartFragment:000000097")
    [GeneratedRegex(@"StartFragment:(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex CfHtmlStartOffsetRegex();

    // EndFragment byte offset from the CF_HTML header
    [GeneratedRegex(@"EndFragment:(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex CfHtmlEndOffsetRegex();

    // ── Combined bold+italic span ────────────────────────────────────────────

    // <span style="...font-weight:bold...font-style:italic..."> (any order)
    [GeneratedRegex(@"<span[^>]*style=""[^""]*font-weight\s*:\s*(?:bold|700)[^""]*font-style\s*:\s*italic[^""]*""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SpanBoldItalicRegex();

    // <span style="...font-style:italic...font-weight:bold..."> (italic first)
    [GeneratedRegex(@"<span[^>]*style=""[^""]*font-style\s*:\s*italic[^""]*font-weight\s*:\s*(?:bold|700)[^""]*""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SpanItalicBoldRegex();

    // <span style="...font-size:..."> — strip tag, keep content (no Markdown equivalent)
    [GeneratedRegex(@"<span[^>]*style=""[^""]*font-size\s*:[^""]*""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SpanFontSizeRegex();

    #endregion
}
