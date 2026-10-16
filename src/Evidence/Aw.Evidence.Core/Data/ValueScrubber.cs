using System.Text.Encodings.Web;
using System.Text.Json;

namespace Aw.Evidence.Core.Data;

/// <summary>
/// A run's scrubber: the secret values the run itself resolved (header values from the environment, pass-through variables) are replaced
/// wherever they appear, in the text and in its JSON-escaped forms, before the pattern scrub runs. A value too short to be told from
/// ordinary text is left to the patterns.
/// </summary>
/// <param name="patterns">The pattern scrubber.</param>
/// <param name="values">The secret values the run resolved.</param>
public sealed class ValueScrubber(ITextScrubber patterns, IEnumerable<string> values) : ITextScrubber
{
    /// <summary>What replaces a resolved value.</summary>
    public const string Placeholder = "[REDACTED:header-value]";

    /// <summary>Values shorter than this are not scrubbed by value: they would hit ordinary words.</summary>
    public const int MinimumLength = 6;

    private readonly string[] _values = [.. values.Where(static v => v.Length >= MinimumLength).SelectMany(Forms).Distinct(StringComparer.Ordinal).OrderByDescending(static v => v.Length)];

    /// <inheritdoc/>
    public string Scrub(string text) =>
        patterns.Scrub(_values.Aggregate(text, static (current, value) => current.Replace(value, Placeholder, StringComparison.Ordinal)));

    /// <inheritdoc/>
    public bool IsSecretName(string name) => patterns.IsSecretName(name);

    private static IEnumerable<string> Forms(string value)
    {
        yield return value;
        yield return JsonEncodedText.Encode(value).ToString();
        yield return JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString();
    }
}
