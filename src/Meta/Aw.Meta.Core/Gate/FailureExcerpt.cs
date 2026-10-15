using System.Text.RegularExpressions;

namespace Aw.Meta.Core.Gate;

/// <summary>
/// The part of a test log that says how one test failed, with the numbers that differ between runs (durations,
/// timestamps) removed, so the same failure on two runs compares equal.
/// </summary>
public static partial class FailureExcerpt
{
    private const int Following = 4;

    /// <summary>Cuts the failure out of a log.</summary>
    /// <param name="log">The log's text.</param>
    /// <param name="test">The test filter: a name the failure lines mention.</param>
    /// <returns>The first line that mentions the test and the lines after it; the last lines of the log when none does.</returns>
    public static string For(string log, string test)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(test);
        var lines = log.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var at = Array.FindIndex(lines, line => line.Contains(test, StringComparison.OrdinalIgnoreCase));
        var picked = at < 0 ? lines.TakeLast(Following + 1) : lines.Skip(at).Take(Following + 1);
        return string.Join('\n', picked.Select(Stable));
    }

    private static string Stable(string line)
    {
        try
        {
            return Volatile.Replace(line, string.Empty).Trim();
        }
        catch (RegexMatchTimeoutException)
        {
            return line;
        }
    }

    [GeneratedRegex(@"\(?\b\d+(\.\d+)?\s?(ms|s)\b\)?|\d{4}-\d{2}-\d{2}[T ][\d:.]+Z?", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Volatile { get; }
}
