namespace Aw.Kernel.Testing;

/// <summary>
/// The normalization a golden comparison declares up front: named rules applied in order, then
/// CRLF to LF. Outputs are compared exactly after it, never by loose matching.
/// </summary>
public sealed class Normalization
{
    /// <summary>Creates a normalization from rules applied in the given order.</summary>
    /// <param name="rules">The rules.</param>
    public Normalization(params IEnumerable<NormalizationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Rules = [.. rules];
    }

    /// <summary>No rules: only line endings are normalized.</summary>
    public static Normalization None { get; } = new();

    /// <summary>ISO-8601 timestamps (with or without fractions, Z or an offset) become '&lt;timestamp&gt;'.</summary>
    public static NormalizationRule IsoTimestamps { get; } = new(
        "isoTimestamps",
        "[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:[.][0-9]+)?(?:Z|[+-][0-9]{2}:[0-9]{2})",
        "<timestamp>");

    /// <summary>GUIDs in 8-4-4-4-12 form become '&lt;guid&gt;'.</summary>
    public static NormalizationRule Guids { get; } = new(
        "guids",
        "[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}",
        "<guid>");

    /// <summary>26-character Crockford ULIDs become '&lt;ulid&gt;'.</summary>
    public static NormalizationRule Ulids { get; } = new(
        "ulids", "(?<![0-9A-Za-z])[0-9A-HJKMNP-TV-Z]{26}(?![0-9A-Za-z])", "<ulid>");

    /// <summary>Durations such as '12ms' or '1.5s' become '&lt;duration&gt;'.</summary>
    public static NormalizationRule Durations { get; } = new(
        "durations", "(?<![0-9A-Za-z.])[0-9]+(?:[.][0-9]+)?(?:ms|s)(?![0-9A-Za-z])", "<duration>");

    /// <summary>The rules, in the order they apply.</summary>
    public IReadOnlyList<NormalizationRule> Rules { get; }

    /// <summary>A rule that replaces one literal path (a test's temp dir) with '&lt;tmp&gt;'.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The rule.</returns>
    public static NormalizationRule TempPath(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return new NormalizationRule("tempPath", System.Text.RegularExpressions.Regex.Escape(path), "<tmp>");
    }

    /// <summary>Applies every rule in order, then turns CRLF into LF.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The normalized text.</returns>
    public string Apply(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = text;
        foreach (var rule in Rules)
        {
            normalized = rule.Apply(normalized);
        }

        return normalized.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
