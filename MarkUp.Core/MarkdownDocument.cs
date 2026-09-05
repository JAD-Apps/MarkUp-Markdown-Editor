namespace MarkUp.Core;

/// <summary>
/// Represents the state of a markdown document.
/// </summary>
public sealed class MarkdownDocument
{
    private string _content = string.Empty;
    private string _filePath = string.Empty;
    private bool _isDirty;

    /// <summary>
    /// Gets or sets the raw markdown content.
    /// </summary>
    public string Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                _isDirty = true;
            }
        }
    }

    /// <summary>
    /// Gets or sets the file path of the document.
    /// </summary>
    public string FilePath
    {
        get => _filePath;
        set => _filePath = value ?? string.Empty;
    }

    /// <summary>
    /// Gets whether the document has unsaved changes.
    /// </summary>
    public bool IsDirty => _isDirty;

    /// <summary>
    /// Line-ending sequence used when the document is written to disk. Detected from the
    /// file on load; new documents default to the Windows convention.
    /// </summary>
    public string LineEnding { get; set; } = "\r\n";

    /// <summary>
    /// Detects the dominant line ending of <paramref name="text"/>: <c>\r\n</c> when present,
    /// otherwise <c>\n</c>, otherwise <c>\r</c>. Falls back to the current value when the
    /// text has no line breaks at all.
    /// </summary>
    public static string DetectLineEnding(string text, string fallback = "\r\n")
    {
        if (string.IsNullOrEmpty(text)) return fallback;
        if (text.Contains("\r\n", StringComparison.Ordinal)) return "\r\n";
        if (text.Contains('\n')) return "\n";
        if (text.Contains('\r')) return "\r";
        return fallback;
    }

    /// <summary>
    /// Returns the content with every line break normalised to <see cref="LineEnding"/>.
    /// The editor control stores typed newlines as a bare <c>\r</c>, so writing
    /// <see cref="Content"/> verbatim would produce files with mixed or CR-only breaks.
    /// </summary>
    public string GetContentForSave()
    {
        return NormalizeLineEndings(_content, LineEnding);
    }

    /// <summary>Normalises all line breaks in <paramref name="text"/> to <paramref name="lineEnding"/>.</summary>
    public static string NormalizeLineEndings(string text, string lineEnding)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var unified = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return lineEnding == "\n" ? unified : unified.Replace("\n", lineEnding);
    }

    /// <summary>
    /// Gets the display name (file name or "Untitled").
    /// </summary>
    public string DisplayName =>
        string.IsNullOrEmpty(_filePath) ? "Untitled" : Path.GetFileName(_filePath);

    /// <summary>
    /// Marks the document as saved (not dirty).
    /// </summary>
    public void MarkSaved()
    {
        _isDirty = false;
    }

    /// <summary>
    /// Resets the document to an empty state.
    /// </summary>
    public void Reset()
    {
        _content = string.Empty;
        _filePath = string.Empty;
        _isDirty = false;
        LineEnding = "\r\n";
    }

    /// <summary>
    /// Gets the window title string for this document.
    /// </summary>
    public string GetWindowTitle()
    {
        var dirtyMarker = _isDirty ? " •" : string.Empty;
        return $"{DisplayName}{dirtyMarker} — MarkUp";
    }

    /// <summary>
    /// Gets statistics about the document content.
    /// </summary>
    public DocumentStatistics GetStatistics()
    {
        return DocumentStatistics.Compute(_content);
    }
}
