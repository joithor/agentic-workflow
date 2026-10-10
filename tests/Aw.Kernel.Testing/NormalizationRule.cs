using System.Text.RegularExpressions;

namespace Aw.Kernel.Testing;

/// <summary>
/// One named rewrite applied to output before it is compared with a golden file: every match of an
/// ASCII-class pattern becomes a fixed placeholder. Matching is culture-invariant and has a timeout.
/// </summary>
public sealed class NormalizationRule
{
    private static readonly TimeSpan _matchTimeout = TimeSpan.FromSeconds(1);
    private readonly Regex _pattern;

    /// <summary>Creates a rule.</summary>
    /// <param name="name">The rule's name, shown in mismatch messages.</param>
    /// <param name="pattern">The regular expression to replace.</param>
    /// <param name="placeholder">What every match becomes, e.g. '&lt;timestamp&gt;'.</param>
    /// <exception cref="ArgumentException">The pattern is not a valid regular expression.</exception>
    public NormalizationRule(string name, string pattern, string placeholder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(placeholder);
        Name = name;
        Placeholder = placeholder;
        _pattern = new Regex(pattern, RegexOptions.CultureInvariant, _matchTimeout);
    }

    /// <summary>The rule's name.</summary>
    public string Name { get; } = string.Empty;

    /// <summary>What every match becomes.</summary>
    public string Placeholder { get; } = string.Empty;

    /// <summary>Applies the rule.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with every match replaced.</returns>
    /// <exception cref="RegexMatchTimeoutException">Matching took longer than a second; the comparison fails.</exception>
    public string Apply(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _pattern.Replace(text, _ => Placeholder);
    }
}
