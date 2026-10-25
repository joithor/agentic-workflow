using System.Text.RegularExpressions;

namespace Aw.Meta.Core.Gate;

/// <summary>
/// The part of a test log that says how one test failed, with the numbers that differ between runs (durations,
/// timestamps) removed, so the same failure on two runs compares equal.
/// </summary>
public static partial class FailureExcerpt
{
    private const int Following = 4;
    private const int MaxFailures = 20;

    /// <summary>Cuts the failure out of a log.</summary>
    /// <param name="log">The log's text.</param>
    /// <param name="test">The test filter: a name the failure lines mention.</param>
    /// <returns>For a bash suite (a test ending in ".sh"), its FAIL and "not ok" lines, which are the same whether they come from the
    /// runner's summary or from the suite's own output; otherwise the first line that mentions the test and the lines after it, or
    /// the last lines of the log when none does.</returns>
    public static string For(string log, string test)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(test);
        var lines = log.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (test.EndsWith(".sh", StringComparison.Ordinal) && SuiteFailures(lines, test) is { Count: > 0 } failures)
        {
            return string.Join('\n', failures.Select(Stable));
        }

        var at = Array.FindIndex(lines, line => line.Contains(test, StringComparison.OrdinalIgnoreCase));
        var picked = at < 0 ? lines.TakeLast(Following + 1) : lines.Skip(at).Take(Following + 1);
        return string.Join('\n', picked.Select(Stable));
    }

    // The bash runner (scripts/ci/bash-tests.sh) summarizes a failed suite as a "FAIL  par|ser  <suite>  (exit N, Ns)" header and the
    // suite's `grep -E 'FAIL|not ok'` lines marked "! ", so they are read from there; a suite's own output (a rerun) has the lines bare.
    // Either way the PASS lines, the runner's tail and stderr noise are left out: they differ between runs of the same failure.
    private static List<string> SuiteFailures(string[] lines, string suite)
    {
        var header = Array.FindIndex(lines, line => line.StartsWith("FAIL ", StringComparison.Ordinal) && line.Contains(suite, StringComparison.Ordinal) && line.Contains("(exit ", StringComparison.Ordinal));
        if (header >= 0)
        {
            return [.. lines.Skip(header + 1).TakeWhile(line => line.StartsWith('!') || line.StartsWith('|')).Where(line => line.StartsWith('!')).Select(line => line[1..].Trim()).Take(MaxFailures)];
        }

        return [.. lines.Where(line => line.StartsWith("FAIL:", StringComparison.Ordinal) || line.StartsWith("not ok", StringComparison.Ordinal)).Take(MaxFailures)];
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
