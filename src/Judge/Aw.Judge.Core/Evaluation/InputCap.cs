using System.Text.Json.Nodes;

namespace Aw.Judge.Core.Evaluation;

/// <summary>
/// Caps stored input and keeps it valid JSON (TS capJsonValue): instead of slicing the text, the longest string values
/// are shortened, each with its own "[truncated N chars]" marker, until the whole fits. Redact first: a secret cut at
/// the cap would no longer match its pattern.
/// </summary>
public static class InputCap
{
    /// <summary>The stored input's cap, in UTF-16 characters.</summary>
    public const int MaxChars = 16_000;

    /// <summary>Writes the value as JSON of at most <paramref name="maxChars"/> characters (for any cap above ~100).</summary>
    /// <param name="value">The (redacted) value; not changed.</param>
    /// <param name="maxChars">The cap.</param>
    /// <returns>Valid JSON text.</returns>
    public static string Write(JsonNode? value, int maxChars)
        => throw new NotSupportedException("red: cap the stored input");

    /// <summary>The text cut to <paramref name="maxChars"/> with a "[truncated N chars]" marker, or unchanged when it fits.</summary>
    /// <param name="text">The text.</param>
    /// <param name="maxChars">How many characters to keep (one fewer when that would split a surrogate pair).</param>
    /// <returns>The capped text.</returns>
    public static string Truncate(string text, int maxChars) => throw new NotSupportedException("red: truncate");
}
