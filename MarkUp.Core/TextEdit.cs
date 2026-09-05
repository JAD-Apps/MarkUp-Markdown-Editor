namespace MarkUp.Core;

/// <summary>
/// Describes the smallest single contiguous replacement that turns one text into another.
/// Used by the editor to apply formatting results as a selection replacement (which the
/// TextBox records on its undo stack) instead of reassigning the whole document (which
/// discards undo history).
/// </summary>
public readonly record struct TextEdit(int Start, int RemovedLength, string InsertedText)
{
    /// <summary>True when the two texts were identical and nothing needs to change.</summary>
    public bool IsEmpty => RemovedLength == 0 && InsertedText.Length == 0;

    /// <summary>
    /// Computes the minimal contiguous edit between <paramref name="oldText"/> and
    /// <paramref name="newText"/> by trimming the common prefix and suffix.
    /// </summary>
    public static TextEdit Compute(string oldText, string newText)
    {
        oldText ??= string.Empty;
        newText ??= string.Empty;

        var minLength = Math.Min(oldText.Length, newText.Length);

        var prefix = 0;
        while (prefix < minLength && oldText[prefix] == newText[prefix])
            prefix++;

        var suffix = 0;
        var maxSuffix = minLength - prefix;
        while (suffix < maxSuffix
               && oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix])
            suffix++;

        var removed = oldText.Length - prefix - suffix;
        var inserted = newText.Substring(prefix, newText.Length - prefix - suffix);
        return new TextEdit(prefix, removed, inserted);
    }
}
