using System.Text;
using System.Text.RegularExpressions;

namespace MarkUp.Core;

/// <summary>
/// Converts markdown text to HTML for preview rendering.
/// This is a self-contained parser — no external dependencies required.
/// </summary>
public static partial class MarkdownParser
{
    /// <summary>
    /// Converts markdown text to a complete HTML document string suitable for WebView2 rendering.
    /// </summary>
    /// <param name="markdown">The markdown source text.</param>
    /// <param name="darkMode">Whether to use dark mode styling.</param>
    /// <param name="editable">Whether to make the preview body contentEditable (WYSIWYG mode).</param>
    /// <param name="documentTitle">Optional document title for the HTML title tag (used in print headers/footers).</param>
    /// <param name="baseHref">Optional base URL for resolving relative image and link paths.</param>
    public static string ToHtml(string markdown, bool darkMode = true, bool editable = false, string documentTitle = "", string baseHref = "")
    {
        if (string.IsNullOrEmpty(markdown))
            return BuildHtmlPage(string.Empty, darkMode, editable, documentTitle, baseHref);

        var body = ConvertBody(markdown);
        return BuildHtmlPage(body, darkMode, editable, documentTitle, baseHref);
    }

    /// <summary>
    /// Converts markdown text to an HTML body fragment (no wrapping document).
    /// The fragment contains no whitespace between block elements: the preview pane maps
    /// rendered text offsets back to the Markdown source, and stray whitespace text nodes
    /// between blocks would shift every offset after them.
    /// </summary>
    public static string ToHtmlFragment(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        return ConvertBody(markdown);
    }

    /// <summary>
    /// Returns true for a line the renderer treats as a task-list item: <c>- [ ] text</c>,
    /// <c>- [x] text</c> or <c>- [X] text</c>. A bullet whose text merely starts with a
    /// bracket (e.g. <c>- [link](url)</c>) is an ordinary list item.
    /// </summary>
    internal static bool IsTaskListItem(string line)
    {
        return line.Length >= 5
               && line[0] == '-' && line[1] == ' ' && line[2] == '['
               && (line[3] == ' ' || line[3] == 'x' || line[3] == 'X')
               && line[4] == ']'
               && (line.Length == 5 || line[5] == ' ');
    }

    private static string ConvertBody(string markdown)
    {
        var taskIndex = 0;
        return ConvertBody(markdown, ref taskIndex);
    }

    private static string ConvertBody(string markdown, ref int taskIndex)
    {
        // Normalize line endings
        var text = markdown.Replace("\r\n", "\n").Replace("\r", "\n");

        var sb = new StringBuilder();
        var lines = text.Split('\n');
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];

            // Blank line
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // Horizontal rule
            if (IsHorizontalRule(line))
            {
                sb.Append("<hr />");
                i++;
                continue;
            }

            // Fenced code block
            if (line.TrimStart().StartsWith("```"))
            {
                var lang = line.TrimStart().Length > 3 ? line.TrimStart()[3..].Trim() : string.Empty;
                i++;
                var codeBlock = new StringBuilder();
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```"))
                {
                    codeBlock.Append(EscapeHtml(lines[i])).Append('\n');
                    i++;
                }
                if (i < lines.Length) i++; // skip closing ```
                var langAttr = !string.IsNullOrEmpty(lang) ? $" class=\"language-{EscapeHtml(lang)}\"" : string.Empty;
                sb.Append($"<pre><code{langAttr}>{codeBlock}</code></pre>");
                continue;
            }

            // Headings
            if (line.StartsWith('#'))
            {
                int level = 0;
                while (level < line.Length && level < 6 && line[level] == '#')
                    level++;
                if (level < line.Length && line[level] == ' ')
                {
                    var headingText = ProcessInline(line[(level + 1)..].Trim());
                    var id = GenerateSlug(line[(level + 1)..].Trim());
                    sb.Append($"<h{level} id=\"{id}\">{headingText}</h{level}>");
                }
                else
                {
                    // '#' without a trailing space (e.g. bare '#', '##', '#NoSpace') is not a
                    // valid ATX heading.  Emit as a paragraph so the outer loop always advances.
                    sb.Append($"<p>{ProcessInline(line)}</p>");
                }
                i++;
                continue;
            }

            // Blockquote
            if (line.StartsWith('>'))
            {
                var quoteLines = new List<string>();
                while (i < lines.Length && lines[i].StartsWith('>'))
                {
                    var ql = lines[i].Length > 1 ? lines[i][1..] : string.Empty;
                    if (ql.StartsWith(' ')) ql = ql[1..];
                    quoteLines.Add(ql);
                    i++;
                }
                var inner = ConvertBody(string.Join("\n", quoteLines), ref taskIndex);
                sb.Append($"<blockquote>{inner}</blockquote>");
                continue;
            }

            // Unordered list
            if (IsUnorderedListItem(line))
            {
                sb.Append("<ul>");
                while (i < lines.Length && IsUnorderedListItem(lines[i]))
                {
                    var itemText = ProcessInline(lines[i][2..].Trim());
                    sb.Append($"<li>{itemText}</li>");
                    i++;
                }
                sb.Append("</ul>");
                continue;
            }

            // Ordered list
            if (IsOrderedListItem(line))
            {
                sb.Append("<ol>");
                while (i < lines.Length && IsOrderedListItem(lines[i]))
                {
                    var dotIndex = lines[i].IndexOf('.');
                    var itemText = ProcessInline(lines[i][(dotIndex + 1)..].Trim());
                    sb.Append($"<li>{itemText}</li>");
                    i++;
                }
                sb.Append("</ol>");
                continue;
            }

            // Table
            if (i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
            {
                var headerCells = ParseTableRow(line);
                i++; // header row
                var separator = lines[i];
                var alignments = ParseTableAlignments(separator);
                i++; // separator row

                sb.Append("<table>");
                sb.Append("<thead><tr>");
                for (int c = 0; c < headerCells.Length; c++)
                {
                    var align = c < alignments.Length ? alignments[c] : string.Empty;
                    var alignAttr = !string.IsNullOrEmpty(align) ? $" style=\"text-align:{align}\"" : string.Empty;
                    sb.Append($"<th{alignAttr}>{ProcessInline(headerCells[c])}</th>");
                }
                sb.Append("</tr></thead>");
                sb.Append("<tbody>");
                while (i < lines.Length && lines[i].Contains('|'))
                {
                    var cells = ParseTableRow(lines[i]);
                    sb.Append("<tr>");
                    for (int c = 0; c < cells.Length; c++)
                    {
                        var align = c < alignments.Length ? alignments[c] : string.Empty;
                        var alignAttr = !string.IsNullOrEmpty(align) ? $" style=\"text-align:{align}\"" : string.Empty;
                        sb.Append($"<td{alignAttr}>{ProcessInline(cells[c])}</td>");
                    }
                    sb.Append("</tr>");
                    i++;
                }
                sb.Append("</tbody></table>");
                continue;
            }

            // Task list (special case of unordered list). The checkbox is a real, clickable
            // control: the preview posts its data-task-index back to the host, which toggles
            // the matching "[ ]"/"[x]" in the Markdown source. No whitespace precedes the item
            // text so rendered offsets line up with the source projection.
            if (IsTaskListItem(line))
            {
                sb.Append("<ul class=\"task-list\">");
                while (i < lines.Length && IsTaskListItem(lines[i]))
                {
                    bool isChecked = lines[i][3] is 'x' or 'X';
                    var itemText = ProcessInline(lines[i][5..].Trim());
                    var checkedAttr = isChecked ? " checked" : string.Empty;
                    sb.Append($"<li><input type=\"checkbox\" class=\"task-checkbox\" data-task-index=\"{taskIndex}\" contenteditable=\"false\"{checkedAttr} />{itemText}</li>");
                    taskIndex++;
                    i++;
                }
                sb.Append("</ul>");
                continue;
            }

            // Setext headings: heading text followed by a line of === (H1) or --- (H2)
            if (i + 1 < lines.Length)
            {
                var underline = lines[i + 1];
                if (underline.Length >= 1 && underline.All(c => c == '='))
                {
                    var headingText = ProcessInline(line.Trim());
                    var id = GenerateSlug(line.Trim());
                    sb.Append($"<h1 id=\"{id}\">{headingText}</h1>");
                    i += 2;
                    continue;
                }
                if (underline.Length >= 1 && underline.All(c => c == '-'))
                {
                    var headingText = ProcessInline(line.Trim());
                    var id = GenerateSlug(line.Trim());
                    sb.Append($"<h2 id=\"{id}\">{headingText}</h2>");
                    i += 2;
                    continue;
                }
            }

            // Paragraph (default)
            {
                var paraLines = new List<string>();
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) &&
                       !lines[i].StartsWith('#') && !lines[i].StartsWith('>') &&
                       !IsUnorderedListItem(lines[i]) && !IsOrderedListItem(lines[i]) && !IsTaskListItem(lines[i]) &&
                       !lines[i].TrimStart().StartsWith("```") && !IsHorizontalRule(lines[i]))
                {
                    paraLines.Add(lines[i]);
                    i++;
                }
                var paraText = ProcessInline(string.Join("\n", paraLines));
                // Convert single newlines within paragraph to <br />
                paraText = paraText.Replace("\n", "<br />\n");
                sb.Append($"<p>{paraText}</p>");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Processes inline markdown elements: bold, italic, code, links, images, strikethrough.
    /// </summary>
    internal static string ProcessInline(string text)
    {
        // Inline code (must come first to protect content; HTML-escape the captured text)
        text = InlineCodeRegex().Replace(text, m => $"<code>{EscapeHtml(m.Groups[1].Value)}</code>");

        // Images
        text = ImageRegex().Replace(text, "<img src=\"$2\" alt=\"$1\" />");

        // Links
        text = LinkRegex().Replace(text, "<a href=\"$2\">$1</a>");

        // Bold + italic
        text = BoldItalicRegex().Replace(text, "<strong><em>$1</em></strong>");

        // Bold
        text = BoldRegex().Replace(text, "<strong>$1</strong>");
        text = BoldUnderscoreRegex().Replace(text, "<strong>$1</strong>");

        // Italic
        text = ItalicRegex().Replace(text, "<em>$1</em>");
        text = ItalicUnderscoreRegex().Replace(text, "<em>$1</em>");

        // Strikethrough
        text = StrikethroughRegex().Replace(text, "<del>$1</del>");

        return text;
    }

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"!\[([^\]]*)\]\(([^\)]+)\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^\)]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"\*\*\*(.+?)\*\*\*")]
    private static partial Regex BoldItalicRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"__(.+?)__")]
    private static partial Regex BoldUnderscoreRegex();

    [GeneratedRegex(@"\*(.+?)\*")]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"_(.+?)_")]
    private static partial Regex ItalicUnderscoreRegex();

    [GeneratedRegex(@"~~(.+?)~~")]
    private static partial Regex StrikethroughRegex();

    private static bool IsHorizontalRule(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3) return false;
        if (trimmed.All(c => c == '-' || c == ' ') && trimmed.Count(c => c == '-') >= 3) return true;
        if (trimmed.All(c => c == '*' || c == ' ') && trimmed.Count(c => c == '*') >= 3) return true;
        if (trimmed.All(c => c == '_' || c == ' ') && trimmed.Count(c => c == '_') >= 3) return true;
        return false;
    }

    private static bool IsUnorderedListItem(string line)
    {
        return (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("+ "))
               && !IsTaskListItem(line);
    }

    private static bool IsOrderedListItem(string line)
    {
        var match = OrderedListRegex().Match(line);
        return match.Success;
    }

    [GeneratedRegex(@"^\d+\.\s")]
    private static partial Regex OrderedListRegex();

    private static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.Contains('|')) return false;
        var cells = trimmed.Split('|', StringSplitOptions.RemoveEmptyEntries);
        return cells.All(c => c.Trim().All(ch => ch == '-' || ch == ':'));
    }

    private static string[] ParseTableRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|')) trimmed = trimmed[1..];
        if (trimmed.EndsWith('|')) trimmed = trimmed[..^1];
        return trimmed.Split('|').Select(c => c.Trim()).ToArray();
    }

    private static string[] ParseTableAlignments(string line)
    {
        var cells = ParseTableRow(line);
        return cells.Select(c =>
        {
            var t = c.Trim();
            if (t.StartsWith(':') && t.EndsWith(':')) return "center";
            if (t.EndsWith(':')) return "right";
            return "left";
        }).ToArray();
    }

    private static string GenerateSlug(string text)
    {
        var slug = text.ToLowerInvariant();
        slug = SlugRegex().Replace(slug, string.Empty);
        slug = slug.Replace(' ', '-');
        return slug;
    }

    [GeneratedRegex(@"[^\w\s-]")]
    private static partial Regex SlugRegex();

    internal static string EscapeHtml(string text)
    {
        return text.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;");
    }

    private static string BuildHtmlPage(string bodyHtml, bool darkMode, bool editable = false, string documentTitle = "", string baseHref = "")
    {
        var bg = darkMode ? "#1e1e1e" : "#ffffff";
        var fg = darkMode ? "#d4d4d4" : "#1e1e1e";
        var codeBg = darkMode ? "#2d2d2d" : "#f5f5f5";
        var borderColor = darkMode ? "#404040" : "#ddd";
        var linkColor = darkMode ? "#569cd6" : "#0066cc";
        var blockquoteBorder = darkMode ? "#569cd6" : "#0066cc";
        var blockquoteBg = darkMode ? "#252525" : "#f9f9f9";
        var toolbarBg = darkMode ? "#252525" : "#f0f0f0";
        var toolbarBorder = darkMode ? "#404040" : "#ccc";

        var editableAttr = editable ? " contenteditable=\"true\"" : string.Empty;
        var baseTag = string.IsNullOrEmpty(baseHref) ? string.Empty : $"<base href=\"{EscapeHtml(baseHref)}\" />";

        var toolbarCss = editable ? $@"
  [contenteditable]:focus {{
    outline: none;
  }}
  ::highlight(sync-highlight) {{
    background-color: rgba(0, 120, 215, 0.3);
    color: inherit;
  }}
  #sync-caret {{
    position: absolute;
    width: 2px;
    background: {linkColor};
    pointer-events: none;
    z-index: 1000;
    animation: sync-caret-blink 1.06s steps(1) infinite;
  }}
  @keyframes sync-caret-blink {{
    0%, 49% {{ opacity: 1; }}
    50%, 100% {{ opacity: 0; }}
  }}
  .task-checkbox {{ cursor: pointer; }}" : @"
  input[type=checkbox] { pointer-events: none; }";

        var editScript = editable ? @"
<script>
  var _suppressNotify = false;
  var debounceTimer;
  var _selectionMessagesSuppressed = false;
  var _cachedTextMap = null;
  var _caretEl = null;
  var _mirroredRange = null;
  var _suppressScrollUntil = 0;

  function suppressSelectionMessages() {
    _selectionMessagesSuppressed = true;
  }

  function invalidateTextMap() {
    _cachedTextMap = null;
  }

  // buildTextMap walks every text node in the document; caching it means repeated
  // selection/caret mirroring during caret movement costs one walk per content
  // change instead of one walk per caret move.
  function getTextMap() {
    if (!_cachedTextMap) _cachedTextMap = buildTextMap();
    return _cachedTextMap;
  }

  function clearMirroredCaret() {
    if (_caretEl) { _caretEl.remove(); _caretEl = null; }
  }

  function clearMirroredSelection() {
    if (typeof CSS !== 'undefined' && CSS.highlights) {
      CSS.highlights.delete('sync-highlight');
    }
    _mirroredRange = null;
    clearMirroredCaret();
  }

  function nodeKey(n) {
    if (n.nodeType === 1) return n.outerHTML;
    if (n.nodeType === 3) return '#' + n.data;
    return '';
  }

  // Called by the C# host to update content without triggering a round-trip sync.
  // Only the top-level blocks that actually changed are replaced: the unchanged
  // prefix and suffix of the document keep their DOM nodes, so layout work is
  // proportional to the edit, the scroll position holds, and selection/highlight
  // ranges in untouched blocks survive.
  function updateContent(html) {
    _suppressNotify = true;
    var body = document.getElementById('editor-body');
    if (body) {
      var tpl = document.createElement('template');
      tpl.innerHTML = html;
      var fresh = Array.prototype.slice.call(tpl.content.childNodes);
      var old = Array.prototype.slice.call(body.childNodes);
      var max = Math.min(old.length, fresh.length);
      var prefix = 0;
      while (prefix < max && nodeKey(old[prefix]) === nodeKey(fresh[prefix])) prefix++;
      var suffix = 0;
      while (suffix < max - prefix && nodeKey(old[old.length - 1 - suffix]) === nodeKey(fresh[fresh.length - 1 - suffix])) suffix++;
      if (!(prefix === old.length && prefix === fresh.length)) {
        var anchor = suffix > 0 ? old[old.length - suffix] : null;
        for (var i = prefix; i < old.length - suffix; i++) body.removeChild(old[i]);
        var frag = document.createDocumentFragment();
        for (var j = prefix; j < fresh.length - suffix; j++) frag.appendChild(fresh[j]);
        body.insertBefore(frag, anchor);
      }
    }
    invalidateTextMap();
    clearMirroredCaret();
    setTimeout(function() { _suppressNotify = false; }, 50);
  }

  // Applies the host zoom level as CSS zoom so both panes scale together.
  function setZoomLevel(percent) {
    document.body.style.zoom = percent + '%';
  }

  // Scrolls the window so rect (viewport coordinates) is visible, with a margin.
  // Programmatic scrolls are marked so the scroll listener does not echo them
  // back to the host as user scrolls.
  function revealRect(rect) {
    if (!rect) return;
    var margin = 40;
    var vh = window.innerHeight;
    var delta = 0;
    if (rect.top < margin) delta = rect.top - margin;
    else if (rect.bottom > vh - margin) delta = rect.bottom - (vh - margin);
    if (delta !== 0) {
      _suppressScrollUntil = Date.now() + 200;
      window.scrollBy(0, delta);
    }
  }

  // Called by the host when the editor scrolls: mirrors the editor's scroll
  // ratio (0..1 of scrollable range) onto the preview document.
  function setScrollRatio(ratio) {
    var se = document.scrollingElement || document.documentElement;
    var max = se.scrollHeight - se.clientHeight;
    if (max <= 0) return;
    _suppressScrollUntil = Date.now() + 200;
    se.scrollTop = ratio * max;
  }

  function notifyChange() {
    if (_suppressNotify) return;
    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(function() {
      var content = document.getElementById('editor-body').innerHTML;
      // Include the current selection offsets so the host can map the edit position
      // back to the Markdown source without an extra async round-trip.
      var selection = getSelectionOffsets(window.getSelection());
      window.chrome.webview.postMessage(JSON.stringify({ type: 'contentChanged', html: content, start: selection.start, length: selection.length }));
    }, 100);
  }

  // ---- Text model -------------------------------------------------------------
  // The host's MarkdownSelectionProjection describes the rendered document as the
  // visible text with exactly one '\n' between consecutive blocks (paragraphs,
  // headings, list items, table rows, quote lines). buildTextMap produces the same
  // string from the live DOM, and every offset that crosses the host boundary is
  // expressed in that model — never in Range.toString() coordinates, which count
  // whitespace between block tags and omit block separators.

  var _blockTags = new Set(['P','H1','H2','H3','H4','H5','H6','LI','BLOCKQUOTE','PRE','TR','DIV']);
  // Whitespace-only text directly inside these containers is layout noise, not content.
  var _structuralTags = new Set(['BODY','UL','OL','TABLE','THEAD','TBODY','TR','BLOCKQUOTE']);

  function blockAncestor(n) {
    var body = document.getElementById('editor-body');
    while (n && n !== body) {
      if (n.nodeType === 1 && _blockTags.has(n.tagName)) return n;
      n = n.parentNode;
    }
    return null;
  }

  function isIgnorableText(tn) {
    if (tn.data.trim() !== '') return false;
    var p = tn.parentNode;
    if (!p) return true;
    if (p.id === 'editor-body') return true;
    return p.nodeType === 1 && _structuralTags.has(p.tagName);
  }

  function buildTextMap() {
    var body = document.getElementById('editor-body');
    if (!body) return null;
    var textNodes = [];
    var nodeOffsets = [];
    var nodeStarts = [];
    var nodeIndex = new Map();
    var fullText = '';
    var prevBlock = null;
    var hasPrev = false;
    var walker = document.createTreeWalker(body, NodeFilter.SHOW_TEXT, null);
    while (walker.nextNode()) {
      var tn = walker.currentNode;
      if (isIgnorableText(tn)) continue;
      var block = blockAncestor(tn);
      if (hasPrev && block !== prevBlock) {
        nodeOffsets.push({ start: fullText.length, len: 1, nodeIdx: -1 });
        fullText += '\n';
      }
      nodeIndex.set(tn, textNodes.length);
      nodeStarts.push(fullText.length);
      nodeOffsets.push({ start: fullText.length, len: tn.data.length, nodeIdx: textNodes.length });
      fullText += tn.data;
      textNodes.push(tn);
      prevBlock = block;
      hasPrev = true;
    }
    return { fullText: fullText, textNodes: textNodes, nodeOffsets: nodeOffsets, nodeStarts: nodeStarts, nodeIndex: nodeIndex };
  }

  // Converts a DOM boundary point to an offset in the text model.
  function domPointToOffset(map, node, offset) {
    if (!map) return 0;
    if (node.nodeType === 3 && map.nodeIndex.has(node)) {
      var ni = map.nodeIndex.get(node);
      return map.nodeStarts[ni] + Math.min(offset, node.data.length);
    }
    var probe = document.createRange();
    try { probe.setStart(node, offset); probe.collapse(true); } catch (e) { return 0; }
    // First mapped text node that starts at or after the point (document order is
    // monotonic, so binary search).
    var lo = 0, hi = map.textNodes.length;
    while (lo < hi) {
      var mid = (lo + hi) >> 1;
      if (probe.comparePoint(map.textNodes[mid], 0) < 0) lo = mid + 1; else hi = mid;
    }
    if (lo > 0) {
      var prev = map.textNodes[lo - 1];
      var prevBlock = blockAncestor(prev);
      // Point sits inside the same block as the previous text (e.g. end of a
      // paragraph): report the end of that text, before the block separator.
      if (prevBlock && prevBlock.contains(node)) return map.nodeStarts[lo - 1] + prev.data.length;
    }
    if (lo >= map.textNodes.length) return map.fullText.length;
    return map.nodeStarts[lo];
  }

  // Returns {start, length} text-model offsets for the current DOM selection.
  // Collapsed carets return length:0 — the host uses these to track cursor position
  // for single-click formatting commands.
  function getSelectionOffsets(sel) {
    if (!sel || sel.rangeCount === 0) return { start: 0, length: 0 };
    var body = document.getElementById('editor-body');
    if (!body) return { start: 0, length: 0 };
    var map = getTextMap();
    if (!map) return { start: 0, length: 0 };
    var range = sel.getRangeAt(0);
    var start = domPointToOffset(map, range.startContainer, range.startOffset);
    var end = range.collapsed ? start : domPointToOffset(map, range.endContainer, range.endOffset);
    if (end < start) { var t = start; start = end; end = t; }
    return { start: start, length: end - start };
  }

  function resolveStartPos(map, charIdx) {
    for (var j = 0; j < map.nodeOffsets.length; j++) {
      var entry = map.nodeOffsets[j];
      if (entry.nodeIdx === -1) continue;
      if (charIdx >= entry.start && charIdx <= entry.start + entry.len) {
        return { node: map.textNodes[entry.nodeIdx], offset: charIdx - entry.start };
      }
      if (charIdx < entry.start) {
        return { node: map.textNodes[entry.nodeIdx], offset: 0 };
      }
    }
    for (var j = map.nodeOffsets.length - 1; j >= 0; j--) {
      var entry = map.nodeOffsets[j];
      if (entry.nodeIdx === -1) continue;
      return { node: map.textNodes[entry.nodeIdx], offset: entry.len };
    }
    return null;
  }

  function resolveEndPos(map, charIdx) {
    for (var j = 0; j < map.nodeOffsets.length; j++) {
      var entry = map.nodeOffsets[j];
      if (entry.nodeIdx === -1) continue;
      if (charIdx >= entry.start && charIdx <= entry.start + entry.len) {
        return { node: map.textNodes[entry.nodeIdx], offset: charIdx - entry.start };
      }
    }

    for (var j = map.nodeOffsets.length - 1; j >= 0; j--) {
      var entry = map.nodeOffsets[j];
      if (entry.nodeIdx === -1) continue;
      if (entry.start + entry.len <= charIdx) {
        return { node: map.textNodes[entry.nodeIdx], offset: entry.len };
      }
    }
    return null;
  }

  // Restores a native DOM selection (or caret) from text-model offsets sent by the host.
  // Called by the host after a preview re-render to place the selection where it was before
  // the re-render, making formatting feel instantaneous from the user's perspective.
  function setSelectionOffsets(start, length) {
    var body = document.getElementById('editor-body');
    if (!body) return;

    var map = getTextMap();
    if (!map) return;

    start = Math.max(0, Math.min(start || 0, map.fullText.length));
    length = Math.max(0, Math.min(length || 0, map.fullText.length - start));

    var startPos = resolveStartPos(map, start);
    if (!startPos) return;

    var endPos = length > 0 ? resolveEndPos(map, start + length) : startPos;
    if (!endPos) return;

    try {
      body.focus();
      var range = new Range();
      range.setStart(startPos.node, startPos.offset);
      if (length > 0) {
        range.setEnd(endPos.node, endPos.offset);
      } else {
        range.collapse(true);
      }

      var sel = window.getSelection();
      if (!sel) return;
      sel.removeAllRanges();
      sel.addRange(range);
    } catch (e) {}
  }

  function setMirroredSelection(start, length, reveal) {
    clearMirroredSelection();
    if (!length || length <= 0) return;

    var map = getTextMap();
    if (!map) return;

    start = Math.max(0, Math.min(start, map.fullText.length));
    var end = Math.max(start, Math.min(start + length, map.fullText.length));
    var startPos = resolveStartPos(map, start);
    var endPos = resolveEndPos(map, end);
    if (startPos && endPos && typeof CSS !== 'undefined' && CSS.highlights) {
      try {
        var range = new Range();
        range.setStart(startPos.node, startPos.offset);
        range.setEnd(endPos.node, endPos.offset);
        CSS.highlights.set('sync-highlight', new Highlight(range));
        _mirroredRange = range;
        if (reveal) revealRect(range.getBoundingClientRect());
      } catch(e) {}
    }
  }

  // Mirrors the editor's collapsed caret into the preview as a blinking caret
  // marker at the corresponding rendered position.
  function setMirroredCaret(start, reveal) {
    clearMirroredSelection();

    var map = getTextMap();
    if (!map) return;

    start = Math.max(0, Math.min(start, map.fullText.length));
    var pos = resolveStartPos(map, start);
    if (!pos) return;

    try {
      var range = document.createRange();
      range.setStart(pos.node, pos.offset);
      range.collapse(true);
      var rect = range.getClientRects()[0] || range.getBoundingClientRect();
      if (!rect || (rect.height === 0 && rect.top === 0 && rect.left === 0)) {
        // Zero rect (e.g. caret between blocks): fall back to the text node's element.
        var el = pos.node.parentElement;
        if (!el) return;
        rect = el.getBoundingClientRect();
      }
      // The caret div lives inside the zoomed body, so its computed lengths are
      // scaled by the body's CSS zoom; divide the physical viewport coordinates
      // by the zoom factor to land on the intended spot.
      var z = parseFloat(document.body.style.zoom) / 100 || 1;
      _caretEl = document.createElement('div');
      _caretEl.id = 'sync-caret';
      _caretEl.style.top = ((rect.top + window.scrollY) / z) + 'px';
      _caretEl.style.left = ((rect.left + window.scrollX) / z) + 'px';
      _caretEl.style.height = ((rect.height || 18) / z) + 'px';
      document.body.appendChild(_caretEl);
      if (reveal) revealRect(rect);
    } catch (e) {}
  }

  // Test/diagnostic hooks -------------------------------------------------------
  // Text currently covered by the mirrored editor selection highlight.
  function getMirroredText() {
    return _mirroredRange ? _mirroredRange.toString() : '';
  }

  // Selects the first occurrence of `text` in the rendered document as a native DOM
  // selection and commits it to the host exactly as a user drag would.
  function selectPreviewText(text) {
    var map = getTextMap();
    if (!map || !text) return false;
    var idx = map.fullText.indexOf(text);
    if (idx < 0) return false;
    _selectionMessagesSuppressed = false;
    setSelectionOffsets(idx, text.length);
    postCommittedSelection(true);
    return true;
  }

  // Posts the current selection (or caret position) to the host as a selectionChanged message.
  // Collapsed carets are included so the host can track cursor position for single-click
  // formatting and for caret restoration after a re-render.
  function postCommittedSelection(force) {
    if (_selectionMessagesSuppressed) return;
    if (!force && !document.hasFocus()) return;

    var sel = window.getSelection();
    if (!sel || sel.rangeCount === 0) return;

    var offsets = getSelectionOffsets(sel);

    window.chrome.webview.postMessage(JSON.stringify({ type: 'selectionChanged', start: offsets.start, length: offsets.length }));
  }

  document.addEventListener('DOMContentLoaded', function() {
    var body = document.getElementById('editor-body');
    if (body) {
      body.addEventListener('input', function(ev) {
        // Checkbox toggles are mirrored to the source precisely via taskToggle; they must
        // not push the whole document through the HTML→Markdown converter.
        if (ev.target && ev.target.type === 'checkbox') return;
        invalidateTextMap();
        notifyChange();
      });
      body.addEventListener('paste', function(e) { setTimeout(notifyChange, 100); });
    }
    document.addEventListener('pointerdown', function() {
      _selectionMessagesSuppressed = false;
      clearMirroredSelection();
    });
    document.addEventListener('keydown', function() {
      _selectionMessagesSuppressed = false;
      clearMirroredSelection();
    });
    document.addEventListener('pointerup', function() { postCommittedSelection(false); });
    document.addEventListener('keyup', function() { postCommittedSelection(false); });

    // Report user scrolls to the host so the editor pane can follow.
    // Programmatic scrolls (setScrollRatio / revealRect) are suppressed via
    // _suppressScrollUntil so host-driven scrolling does not echo back.
    var scrollRafPending = false;
    window.addEventListener('scroll', function() {
      if (scrollRafPending) return;
      scrollRafPending = true;
      requestAnimationFrame(function() {
        scrollRafPending = false;
        if (Date.now() < _suppressScrollUntil) return;
        var se = document.scrollingElement || document.documentElement;
        var max = se.scrollHeight - se.clientHeight;
        var ratio = max > 0 ? se.scrollTop / max : 0;
        window.chrome.webview.postMessage(JSON.stringify({ type: 'scrollChanged', ratio: ratio }));
      });
    }, { passive: true });
  });
  document.addEventListener('click', function(e) {
    var cb = e.target;
    if (cb && cb.tagName === 'INPUT' && cb.type === 'checkbox' && cb.hasAttribute('data-task-index')) {
      // Let the browser toggle the box, then mirror the state into the attribute (so
      // outerHTML comparisons and HTML→Markdown conversion see it) and tell the host.
      setTimeout(function() {
        if (cb.checked) cb.setAttribute('checked', ''); else cb.removeAttribute('checked');
        window.chrome.webview.postMessage(JSON.stringify({ type: 'taskToggle', index: parseInt(cb.getAttribute('data-task-index'), 10), checked: !!cb.checked }));
      }, 0);
      return;
    }
    var link = e.target.closest('a');
    if (!link) return;
    var href = link.getAttribute('href');
    if (href && href.startsWith('#')) {
      e.preventDefault();
      var target = document.getElementById(href.substring(1));
      if (target) target.scrollIntoView({ behavior: 'smooth' });
      return;
    }
    if (e.ctrlKey) {
      e.preventDefault();
      e.stopPropagation();
      window.chrome.webview.postMessage(JSON.stringify({ type: 'openLink', url: link.href }));
    } else {
      e.preventDefault();
    }
  });
</script>" : @"<script>
  document.addEventListener('click', function(e) {
    var link = e.target.closest('a');
    if (!link) return;
    var href = link.getAttribute('href');
    if (href && href.startsWith('#')) {
      e.preventDefault();
      var target = document.getElementById(href.substring(1));
      if (target) target.scrollIntoView({ behavior: 'smooth' });
      return;
    }
    if (e.ctrlKey) {
      e.preventDefault();
      e.stopPropagation();
      window.chrome.webview.postMessage(JSON.stringify({ type: 'openLink', url: link.href }));
    } else {
      e.preventDefault();
    }
  });
</script>";

        var safeTitle = string.IsNullOrEmpty(documentTitle) ? "MarkUp Document" : EscapeHtml(documentTitle);

        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
<meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
{baseTag}
<title>{safeTitle}</title>
<style>
  * {{ margin: 0; padding: 0; box-sizing: border-box; }}
  body {{
    font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif;
    font-size: 15px;
    line-height: 1.7;
    color: {fg};
    background-color: {bg};
  }}
  #editor-body {{
    padding: 24px 32px;
    min-height: calc(100vh - 40px);
  }}
  h1, h2, h3, h4, h5, h6 {{
    margin-top: 1.4em;
    margin-bottom: 0.6em;
    font-weight: 600;
    line-height: 1.3;
  }}
  h1 {{ font-size: 2em; border-bottom: 2px solid {borderColor}; padding-bottom: 0.3em; }}
  h2 {{ font-size: 1.5em; border-bottom: 1px solid {borderColor}; padding-bottom: 0.3em; }}
  h3 {{ font-size: 1.25em; }}
  h4 {{ font-size: 1.1em; }}
  p {{ margin-bottom: 1em; }}
  a {{ color: {linkColor}; text-decoration: none; cursor: pointer; position: relative; }}
  a:hover {{ text-decoration: underline; }}
  a::after {{
    content: 'Ctrl+Click to follow link';
    position: absolute;
    bottom: 100%;
    left: 50%;
    transform: translateX(-50%);
    background: {toolbarBg};
    color: {fg};
    border: 1px solid {toolbarBorder};
    border-radius: 4px;
    padding: 4px 8px;
    font-size: 11px;
    white-space: nowrap;
    pointer-events: none;
    opacity: 0;
    transition: opacity 0.15s;
    z-index: 1000;
  }}
  a:hover::after {{ opacity: 1; }}
  code {{
    font-family: 'Cascadia Code', 'Consolas', monospace;
    background: {codeBg};
    padding: 2px 6px;
    border-radius: 4px;
    font-size: 0.9em;
  }}
  pre {{
    background: {codeBg};
    padding: 16px;
    border-radius: 6px;
    overflow-x: auto;
    margin-bottom: 1em;
    border: 1px solid {borderColor};
  }}
  pre code {{
    background: none;
    padding: 0;
    font-size: 0.9em;
  }}
  blockquote {{
    border-left: 4px solid {blockquoteBorder};
    background: {blockquoteBg};
    padding: 12px 20px;
    margin: 1em 0;
    border-radius: 0 6px 6px 0;
  }}
  blockquote p {{ margin-bottom: 0.5em; }}
  ul, ol {{ margin: 0.5em 0 1em 2em; }}
  li {{ margin-bottom: 0.3em; }}
  .task-list {{ list-style: none; padding-left: 0; }}
  .task-list li {{ display: flex; align-items: baseline; gap: 8px; }}
  hr {{
    border: none;
    border-top: 1px solid {borderColor};
    margin: 2em 0;
  }}
  table {{
    border-collapse: collapse;
    width: 100%;
    margin: 1em 0;
  }}
  th, td {{
    border: 1px solid {borderColor};
    padding: 8px 12px;
    text-align: left;
  }}
  th {{ background: {codeBg}; font-weight: 600; }}
  img {{ max-width: 100%; height: auto; border-radius: 4px; margin: 0.5em 0; }}
  del {{ opacity: 0.6; }}
  strong {{ font-weight: 700; }}
  {toolbarCss}
  @media print {{
    body {{ background-color: #fff !important; }}
    #editor-body {{ padding: 0 !important; max-width: 100% !important; }}
    h1, h2, h3, h4, h5, h6 {{ color: #000 !important; }}
    h1 {{ border-bottom-color: #ccc !important; }}
    h2 {{ border-bottom-color: #ccc !important; }}
    p, li, td, th {{ color: #000 !important; }}
    a {{ color: #0066cc !important; text-decoration: underline !important; }}
    a::after {{ display: none !important; }}
    code {{ background: #f0f0f0 !important; color: #000 !important; }}
    pre {{
      background: #f5f5f5 !important; color: #000 !important; border-color: #ddd !important;
      overflow-x: visible !important;
      white-space: pre-wrap !important;
      overflow-wrap: anywhere !important;
      word-break: break-word !important;
    }}
    pre code {{ color: #000 !important; white-space: pre-wrap !important; overflow-wrap: anywhere !important; }}
    code {{ overflow-wrap: anywhere !important; }}
    table {{ table-layout: fixed !important; width: 100% !important; }}
    th, td {{ overflow-wrap: anywhere !important; word-break: break-word !important; }}
    blockquote {{ border-left-color: #999 !important; background: #f9f9f9 !important; color: #333 !important; }}
    th {{ background: #eee !important; color: #000 !important; }}
    td {{ background: #fff !important; color: #000 !important; border-color: #999 !important; }}
    th {{ border-color: #999 !important; }}
    hr {{ border-top-color: #ccc !important; }}
    del {{ color: #666 !important; }}
    strong {{ color: #000 !important; }}
    em {{ color: #000 !important; }}
    #sync-caret {{ display: none !important; }}
    /* Keep small blocks intact, but let code blocks and tables break across
       pages — page-break-inside: avoid on a block taller than one page clips
       its overflow instead of continuing on the next page. */
    blockquote, img {{ page-break-inside: avoid; }}
    pre, table {{ page-break-inside: auto; }}
    tr {{ page-break-inside: avoid; }}
    h1, h2, h3 {{ page-break-after: avoid; }}
  }}
</style>
</head>
<body>
<div id=""editor-body""{editableAttr}>{bodyHtml}</div>
{editScript}
</body>
</html>";
    }

    /// <summary>
    /// Builds an HTML page optimized for printing (light theme, print styles).
    /// </summary>
    public static string ToHtmlForPrint(string markdown, string documentTitle = "", string baseHref = "")
    {
        if (string.IsNullOrEmpty(markdown))
            return BuildPrintHtmlPage(string.Empty, documentTitle, baseHref);

        var body = ConvertBody(markdown);
        return BuildPrintHtmlPage(body, documentTitle, baseHref);
    }

    private static string BuildPrintHtmlPage(string bodyHtml, string documentTitle, string baseHref)
    {
        var safeTitle = string.IsNullOrEmpty(documentTitle) ? "MarkUp Document" : EscapeHtml(documentTitle);
        var baseTag = string.IsNullOrEmpty(baseHref) ? string.Empty : $"<base href=\"{EscapeHtml(baseHref)}\" />";
        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
{baseTag}
<title>{safeTitle}</title>
<style>
  * {{ margin: 0; padding: 0; box-sizing: border-box; }}
  body {{
    font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif;
    font-size: 12pt;
    line-height: 1.6;
    color: #000 !important;
    background: #fff !important;
    padding: 0;
    max-width: 100%;
    -webkit-print-color-adjust: exact;
    print-color-adjust: exact;
  }}
  h1, h2, h3, h4, h5, h6 {{
    margin-top: 1.2em;
    margin-bottom: 0.5em;
    font-weight: 600;
    line-height: 1.3;
    page-break-after: avoid;
    color: #000 !important;
  }}
  h1 {{ font-size: 24pt; border-bottom: 2px solid #ccc; padding-bottom: 4pt; }}
  h2 {{ font-size: 18pt; border-bottom: 1px solid #ccc; padding-bottom: 3pt; }}
  h3 {{ font-size: 14pt; }}
  p {{ margin-bottom: 0.8em; color: #000 !important; }}
  a {{ color: #0066cc !important; text-decoration: underline; }}
  code {{
    font-family: 'Consolas', monospace;
    background: #f0f0f0 !important;
    color: #000 !important;
    padding: 1px 4px;
    border-radius: 3px;
    font-size: 10pt;
    overflow-wrap: anywhere;
  }}
  pre {{
    background: #f5f5f5 !important;
    color: #000 !important;
    padding: 12px;
    border-radius: 4px;
    border: 1px solid #ddd;
    margin-bottom: 1em;
    white-space: pre-wrap;
    overflow-wrap: anywhere;
    word-break: break-word;
    page-break-inside: auto;
  }}
  pre code {{ background: none !important; padding: 0; color: #000 !important; white-space: pre-wrap; overflow-wrap: anywhere; }}
  blockquote {{
    border-left: 3px solid #999;
    padding: 8px 16px;
    margin: 0.8em 0;
    color: #333 !important;
  }}
  ul, ol {{ margin: 0.5em 0 1em 2em; color: #000 !important; }}
  li {{ margin-bottom: 0.2em; color: #000 !important; }}
  .task-list {{ list-style: none; padding-left: 0; }}
  hr {{ border: none; border-top: 1px solid #ccc; margin: 1.5em 0; }}
  table {{ border-collapse: collapse; width: 100%; margin: 1em 0; table-layout: fixed; page-break-inside: auto; }}
  tr {{ page-break-inside: avoid; }}
  th, td {{ border: 1px solid #999; padding: 6px 10px; text-align: left; color: #000 !important; overflow-wrap: anywhere; word-break: break-word; }}
  th {{ background: #eee !important; font-weight: 600; color: #000 !important; }}
  td {{ background: #fff !important; }}
  img {{ max-width: 100%; height: auto; }}
  del {{ color: #666 !important; opacity: 0.7; }}
  strong {{ color: #000 !important; }}
  em {{ color: #000 !important; }}
  @media print {{
    body {{ padding: 0; color: #000 !important; background: #fff !important; }}
    * {{ color: #000 !important; }}
    a {{ color: #0066cc !important; }}
    blockquote, img {{ page-break-inside: avoid; }}
    pre, table {{ page-break-inside: auto; }}
    tr {{ page-break-inside: avoid; }}
    h1, h2, h3 {{ page-break-after: avoid; }}
    code {{ background: #f0f0f0 !important; }}
    pre {{ background: #f5f5f5 !important; }}
    th {{ background: #eee !important; }}
    td {{ background: #fff !important; }}
  }}
</style>
</head>
<body>
{bodyHtml}
</body>
</html>";
    }
}
