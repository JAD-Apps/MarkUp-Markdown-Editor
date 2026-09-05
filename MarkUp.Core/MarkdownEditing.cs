using System.Text;
using System.Text.RegularExpressions;

namespace MarkUp.Core;

/// <summary>
/// Keyboard-driven editing behaviours for the Markdown source editor: list continuation on
/// Enter, Tab/Shift+Tab indentation, and task-item toggling. All methods are pure functions
/// over the document text so they can be unit tested without the UI. Line breaks may be
/// <c>\n</c>, <c>\r\n</c> or a bare <c>\r</c> (the WinUI TextBox reports typed newlines as
/// <c>\r</c>); existing breaks are always preserved verbatim.
/// </summary>
public static partial class MarkdownEditing
{
    /// <summary>Two-space indentation unit used for lists and Tab insertion.</summary>
    public const string IndentUnit = "  ";

    /// <summary>
    /// Handles Enter pressed at <paramref name="caretPosition"/>. When the caret's line is a
    /// bullet, numbered, task or blockquote item the new line is started with the same marker
    /// (numbers increment). Pressing Enter on an item that has no content removes the marker
    /// instead, ending the list. Returns <see langword="null"/> when the line is not a list
    /// item and the caller should let the default newline behaviour run.
    /// </summary>
    public static FormattingResult? ContinueListOnEnter(string text, int caretPosition)
    {
        if (string.IsNullOrEmpty(text)) return null;
        caretPosition = Math.Clamp(caretPosition, 0, text.Length);

        var lineStart = MarkdownFormatter.GetLineStart(text, caretPosition);
        var lineEnd = MarkdownFormatter.GetLineEnd(text, caretPosition);
        var line = text[lineStart..lineEnd];

        var match = ListMarkerRegex().Match(line);
        if (!match.Success) return null;

        var markerEnd = match.Groups["marker"].Index + match.Groups["marker"].Length;
        var caretColumn = caretPosition - lineStart;
        if (caretColumn < markerEnd) return null; // caret inside the marker: plain newline

        var rest = line[markerEnd..];
        var indent = match.Groups["indent"].Value;

        if (rest.Trim().Length == 0)
        {
            // Empty item: remove the marker (and the item's trailing whitespace) to end the list.
            var newText = text[..lineStart] + indent + text[lineEnd..];
            return new FormattingResult(newText, lineStart + indent.Length, 0);
        }

        string nextMarker;
        if (match.Groups["task"].Success)
        {
            nextMarker = match.Groups["bullet"].Value + " [ ] ";
        }
        else if (match.Groups["bullet"].Success)
        {
            nextMarker = match.Groups["bullet"].Value + " ";
        }
        else if (match.Groups["number"].Success)
        {
            var number = long.TryParse(match.Groups["number"].Value, out var n) ? n + 1 : 1;
            nextMarker = number + match.Groups["delimiter"].Value + " ";
        }
        else
        {
            nextMarker = "> ";
        }

        // Text after the caret moves to the new item; drop the spaces that separated it.
        var tailStart = caretPosition;
        while (tailStart < lineEnd && text[tailStart] == ' ') tailStart++;

        var insertion = "\n" + indent + nextMarker;
        var result = text[..caretPosition] + insertion + text[tailStart..];
        return new FormattingResult(result, caretPosition + insertion.Length, 0);
    }

    /// <summary>
    /// Handles Tab. With no selection on a non-list line, inserts <see cref="IndentUnit"/> at
    /// the caret. Otherwise indents every line touched by the selection by one unit.
    /// </summary>
    public static FormattingResult IndentLines(string text, int selectionStart, int selectionLength)
    {
        text ??= string.Empty;
        selectionStart = Math.Clamp(selectionStart, 0, text.Length);
        selectionLength = Math.Clamp(selectionLength, 0, text.Length - selectionStart);

        if (selectionLength == 0)
        {
            var lineStart = MarkdownFormatter.GetLineStart(text, selectionStart);
            var lineEnd = MarkdownFormatter.GetLineEnd(text, selectionStart);
            if (!ListMarkerRegex().IsMatch(text[lineStart..lineEnd]))
            {
                var inserted = text[..selectionStart] + IndentUnit + text[selectionStart..];
                return new FormattingResult(inserted, selectionStart + IndentUnit.Length, 0);
            }
        }

        var lineStarts = GetAffectedLineStarts(text, selectionStart, selectionLength);
        var sb = new StringBuilder(text.Length + lineStarts.Count * IndentUnit.Length);
        var copied = 0;
        foreach (var ls in lineStarts)
        {
            sb.Append(text, copied, ls - copied);
            sb.Append(IndentUnit);
            copied = ls;
        }
        sb.Append(text, copied, text.Length - copied);

        var newText = sb.ToString();
        if (selectionLength == 0)
            return new FormattingResult(newText, selectionStart + IndentUnit.Length, 0);

        var firstLineStart = lineStarts[0];
        var newEnd = selectionStart + selectionLength + lineStarts.Count * IndentUnit.Length;
        return new FormattingResult(newText, firstLineStart, newEnd - firstLineStart);
    }

    /// <summary>
    /// Handles Shift+Tab: removes up to one indentation unit (two spaces, or a tab) from the
    /// start of every line touched by the selection.
    /// </summary>
    public static FormattingResult OutdentLines(string text, int selectionStart, int selectionLength)
    {
        text ??= string.Empty;
        selectionStart = Math.Clamp(selectionStart, 0, text.Length);
        selectionLength = Math.Clamp(selectionLength, 0, text.Length - selectionStart);

        var lineStarts = GetAffectedLineStarts(text, selectionStart, selectionLength);
        var sb = new StringBuilder(text.Length);
        var copied = 0;
        var removed = 0;
        var removedOnFirstLine = 0;
        foreach (var ls in lineStarts)
        {
            var lineEnd = MarkdownFormatter.GetLineEnd(text, ls);
            var strip = 0;
            if (ls < lineEnd && text[ls] == '\t') strip = 1;
            else
            {
                while (strip < IndentUnit.Length && ls + strip < lineEnd && text[ls + strip] == ' ')
                    strip++;
            }

            sb.Append(text, copied, ls - copied);
            copied = ls + strip;
            removed += strip;
            if (ls == lineStarts[0]) removedOnFirstLine = strip;
        }
        sb.Append(text, copied, text.Length - copied);

        var newText = sb.ToString();
        var newStart = Math.Max(lineStarts[0], selectionStart - removedOnFirstLine);
        if (selectionLength == 0)
            return new FormattingResult(newText, newStart, 0);

        var newEnd = Math.Max(newStart, selectionStart + selectionLength - removed);
        return new FormattingResult(newText, newStart, newEnd - newStart);
    }

    /// <summary>
    /// Toggles the checked state of the <paramref name="taskIndex"/>-th task-list item in
    /// document order (zero-based, counting only lines the renderer treats as task items,
    /// including those nested in blockquotes). Returns <see langword="null"/> when no such
    /// item exists.
    /// </summary>
    public static FormattingResult? ToggleTaskItem(string text, int taskIndex)
    {
        if (string.IsNullOrEmpty(text) || taskIndex < 0) return null;

        var pos = 0;
        var seen = 0;
        while (pos <= text.Length)
        {
            var lineEnd = MarkdownFormatter.GetLineEnd(text, pos);
            var line = text[pos..lineEnd];

            // Blockquoted task items render as tasks too, so count them in the same order.
            var contentOffset = 0;
            while (contentOffset < line.Length && line[contentOffset] == '>')
            {
                contentOffset++;
                if (contentOffset < line.Length && line[contentOffset] == ' ') contentOffset++;
            }

            if (MarkdownParser.IsTaskListItem(line[contentOffset..]))
            {
                if (seen == taskIndex)
                {
                    var boxIndex = pos + contentOffset + 3; // "- [" + state char
                    var current = text[boxIndex];
                    var replacement = current == ' ' ? 'x' : ' ';
                    var newText = text[..boxIndex] + replacement + text[(boxIndex + 1)..];
                    return new FormattingResult(newText, boxIndex, 1);
                }
                seen++;
            }

            if (lineEnd >= text.Length) break;
            pos = MarkdownFormatter.GetNextLineStart(text, lineEnd);
        }

        return null;
    }

    /// <summary>Start offsets of every line the selection touches, in order (never empty).</summary>
    private static List<int> GetAffectedLineStarts(string text, int selectionStart, int selectionLength)
    {
        var starts = new List<int>();
        var selectionEnd = selectionStart + selectionLength;
        // A selection that ends exactly at the start of a line does not include that line.
        if (selectionLength > 0 && selectionEnd > 0 && (text[selectionEnd - 1] == '\n' || text[selectionEnd - 1] == '\r'))
            selectionEnd--;

        var pos = MarkdownFormatter.GetLineStart(text, selectionStart);
        while (true)
        {
            starts.Add(pos);
            var lineEnd = MarkdownFormatter.GetLineEnd(text, pos);
            if (lineEnd >= text.Length || lineEnd >= selectionEnd) break;
            pos = MarkdownFormatter.GetNextLineStart(text, lineEnd);
            if (pos > selectionEnd) break;
        }
        return starts;
    }

    // indent + ( bullet [task] | number delimiter | '>' ) followed by at least one space.
    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<marker>(?:(?<bullet>[-*+])[ ]+(?<task>\[[ xX]\][ ]+)?)|(?:(?<number>\d+)(?<delimiter>[.)])[ ]+)|(?:>[ ]?))")]
    private static partial Regex ListMarkerRegex();
}
