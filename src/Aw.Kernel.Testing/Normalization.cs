namespace Aw.Kernel.Testing;

/// <summary>
/// The normalization a golden comparison declares up front (spec §4c): named rules applied in order, then
/// CRLF to LF. Outputs are compared exactly after it, never by loose matching.
/// </summary>
public sealed class Normalization
{
    /// <summary>Creates a normalization from rules applied in the given order.</summary>
    /// <param name="rules">The rules.</param>
    public Normalization(params IEnumerable<NormalizationRule> rules)
    {
        throw new NotSupportedException("red: Aw.Kernel.Testing C2");
    }

    /// <summary>No rules: only line endings are normalized.</summary>
    public static Normalization None => throw new NotSupportedException("red: Aw.Kernel.Testing C2");

    /// <summary>ISO-8601 timestamps (with or without fractions, Z or an offset) become '&lt;timestamp&gt;'.</summary>
    public static NormalizationRule IsoTimestamps => throw new NotSupportedException("red: Aw.Kernel.Testing C2");

    /// <summary>GUIDs in 8-4-4-4-12 form become '&lt;guid&gt;'.</summary>
    public static NormalizationRule Guids => throw new NotSupportedException("red: Aw.Kernel.Testing C2");

    /// <summary>26-character Crockford ULIDs become '&lt;ulid&gt;'.</summary>
    public static NormalizationRule Ulids => throw new NotSupportedException("red: Aw.Kernel.Testing C2");

    /// <summary>Durations such as '12ms' or '1.5s' become '&lt;duration&gt;'.</summary>
    public static NormalizationRule Durations => throw new NotSupportedException("red: Aw.Kernel.Testing C2");

    /// <summary>The rules, in the order they apply.</summary>
    public IReadOnlyList<NormalizationRule> Rules { get; } = [];

    /// <summary>A rule that replaces one literal path (a test's temp dir) with '&lt;tmp&gt;'.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The rule.</returns>
    public static NormalizationRule TempPath(string path) => throw new NotSupportedException("red: Aw.Kernel.Testing C2");

    /// <summary>Applies every rule in order, then turns CRLF into LF.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The normalized text.</returns>
    public string Apply(string text) => throw new NotSupportedException("red: Aw.Kernel.Testing C2");
}
