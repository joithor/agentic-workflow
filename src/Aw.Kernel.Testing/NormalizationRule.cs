namespace Aw.Kernel.Testing;

/// <summary>
/// One named rewrite applied to output before it is compared with a golden file: every match of an
/// ASCII-class pattern becomes a fixed placeholder. Matching is culture-invariant and has a timeout (spec §4c A2).
/// </summary>
public sealed class NormalizationRule
{
    /// <summary>Creates a rule.</summary>
    /// <param name="name">The rule's name, shown in mismatch messages.</param>
    /// <param name="pattern">The regular expression to replace.</param>
    /// <param name="placeholder">What every match becomes, e.g. '&lt;timestamp&gt;'.</param>
    public NormalizationRule(string name, string pattern, string placeholder)
    {
        throw new NotSupportedException("red: Aw.Kernel.Testing C2");
    }

    /// <summary>The rule's name.</summary>
    public string Name { get; } = string.Empty;

    /// <summary>What every match becomes.</summary>
    public string Placeholder { get; } = string.Empty;

    /// <summary>Applies the rule.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with every match replaced.</returns>
    public string Apply(string text) => throw new NotSupportedException("red: Aw.Kernel.Testing C2");
}
