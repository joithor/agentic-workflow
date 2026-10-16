using System.Text.Encodings.Web;
using System.Text.Json;
using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>
/// A run's scrubber: the secret values the run itself resolved (header values from the environment, pass-through variables) are replaced
/// wherever they appear, in the text and in its JSON-escaped forms, before the pattern scrub runs. A value too short to be told from
/// ordinary text is left to the patterns.
/// </summary>
internal sealed class ValueScrubber : ITextScrubber
{
    /// <summary>What replaces a resolved value.</summary>
    internal const string Placeholder = "[REDACTED:header-value]";

    /// <summary>Values shorter than this are not scrubbed by value: they would hit ordinary words.</summary>
    internal const int MinimumLength = 6;

    private readonly ITextScrubber _patterns;
    private readonly string[] _values;

    internal ValueScrubber(ITextScrubber patterns, IEnumerable<string> values)
    {
        _patterns = patterns;
        _values = [.. values.Where(static v => v.Length >= MinimumLength).SelectMany(Forms).Distinct(StringComparer.Ordinal).OrderByDescending(static v => v.Length)];
    }

    /// <inheritdoc/>
    public string Scrub(string text) =>
        _patterns.Scrub(_values.Aggregate(text, static (current, value) => current.Replace(value, Placeholder, StringComparison.Ordinal)));

    /// <inheritdoc/>
    public bool IsSecretName(string name) => _patterns.IsSecretName(name);

    private static IEnumerable<string> Forms(string value)
    {
        yield return value;
        yield return JsonEncodedText.Encode(value).ToString();
        yield return JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString();
    }
}
