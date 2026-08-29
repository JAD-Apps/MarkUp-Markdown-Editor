using System.Text;
using System.Text.RegularExpressions;

namespace MarkUp.Core;

/// <summary>
/// Exports markdown documents to various formats.
/// </summary>
public static partial class DocumentExporter
{
    /// <summary>
    /// Exports markdown content as an HTML file string.
    /// </summary>
    public static string ExportToHtml(string markdownContent, bool darkMode = false)
    {
        return MarkdownParser.ToHtml(markdownContent, darkMode);
    }

    /// <summary>
    /// Strips markdown formatting and returns plain text.
    /// </summary>
    public static string ExportToPlainText(string markdownContent)
    {
        if (string.IsNullOrEmpty(markdownContent))
            return string.Empty;

        // Process line-by-line so fenced code block content passes through verbatim —
        // stripping emphasis markers inside code would corrupt source code like
        // pointers (`**p`) or Python `**kwargs`.
        var lines = markdownContent.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var sb = new StringBuilder(markdownContent.Length);
        var inFence = false;

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue; // drop the fence line itself
            }

            sb.Append(inFence ? line : StripInlineMarkers(line));
            sb.Append('\n');
        }

        var text = sb.ToString();

        // Clean up extra blank lines
        text = MultipleBlankLines().Replace(text, "\n\n");

        return text.Trim();
    }

    private static string StripInlineMarkers(string text)
    {
        // Remove images (keep alt text)
        text = ImagePattern().Replace(text, "$1");
        // Remove links (keep link text)
        text = LinkPattern().Replace(text, "$1");
        // Remove inline code markers (keep code content, before emphasis stripping)
        text = InlineCodePattern().Replace(text, "$1");
        // Remove bold/italic/strikethrough markers
        text = text.Replace("***", string.Empty);
        text = text.Replace("**", string.Empty);
        text = text.Replace("~~", string.Empty);
        text = ItalicStarPattern().Replace(text, "$1");
        text = text.Replace("__", string.Empty);
        text = ItalicUnderscorePattern().Replace(text, "$1");
        // Remove heading markers
        text = HeadingPattern().Replace(text, "$1");
        // Remove blockquote markers
        text = BlockquotePattern().Replace(text, "$1");
        return text;
    }

    [GeneratedRegex(@"!\[([^\]]*)\]\([^\)]+\)")]
    private static partial Regex ImagePattern();

    [GeneratedRegex(@"\[([^\]]+)\]\([^\)]+\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"^#{1,6}\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodePattern();

    [GeneratedRegex(@"^>\s*(.*)$", RegexOptions.Multiline)]
    private static partial Regex BlockquotePattern();

    [GeneratedRegex(@"\*([^*\n]+)\*")]
    private static partial Regex ItalicStarPattern();

    // Underscore emphasis must not fire inside identifiers like snake_case_name,
    // so both delimiters require a non-word neighbour on the outside.
    [GeneratedRegex(@"(?<![\w])_([^_\n]+)_(?![\w])")]
    private static partial Regex ItalicUnderscorePattern();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleBlankLines();
}
