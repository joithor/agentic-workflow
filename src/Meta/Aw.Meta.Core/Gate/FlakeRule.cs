using System.Text.RegularExpressions;

namespace Aw.Meta.Core.Gate;

/// <summary>Classifies a red gate test as a flake or a real failure.</summary>
public static partial class FlakeRule
{
    /// <summary>Classifies the test.</summary>
    /// <param name="alone">The test rerun alone on the gate tree.</param>
    /// <param name="onMain">The test run on a plain main tree.</param>
    /// <param name="gateFailure">The failure text from the gate run.</param>
    /// <returns>Flake when it passes alone or fails the same way on main; otherwise real.</returns>
    public static FlakeVerdict Classify(RerunOutcome alone, RerunOutcome onMain, string gateFailure)
    {
        ArgumentNullException.ThrowIfNull(alone);
        ArgumentNullException.ThrowIfNull(onMain);
        if (alone.Passed)
        {
            return FlakeVerdict.Flake;
        }

        return !onMain.Passed && Normalize(onMain.Failure) == Normalize(gateFailure)
            ? FlakeVerdict.Flake
            : FlakeVerdict.Real;
    }

    // Whitespace runs differ between runs of the same failure (indentation, line wraps).
    private static string Normalize(string? text)
    {
        try
        {
            return WhiteSpace.Replace(text ?? string.Empty, " ").Trim();
        }
        catch (RegexMatchTimeoutException)
        {
            return text ?? string.Empty;
        }
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex WhiteSpace { get; }
}
