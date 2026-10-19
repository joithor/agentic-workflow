using System.Text.RegularExpressions;

namespace Aw.Meta.Core.Gate;

/// <summary>Reads the failing tests out of a Microsoft.Testing.Platform log or the bash test runner's log and matches test names against filters.</summary>
public static partial class TestNames
{
    /// <summary>The failing tests a log names: MTP prints "failed &lt;Namespace.Class.Method(args)&gt; (12ms)" for each.</summary>
    /// <param name="log">The step log's text.</param>
    /// <returns>The distinct failing test names, in order of appearance.</returns>
    public static IReadOnlyList<string> Failing(string log)
    {
        ArgumentNullException.ThrowIfNull(log);
        var names = new List<string>();
        foreach (var line in log.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (Match(line) is { } name && !names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>How many tests the log's summaries say failed: the "failed: N" lines added up (one summary per test project), or the
    /// per-assembly "&lt;dll&gt; (tfm|arch) failed with N error(s)" lines added up (each assembly once: MTP prints it when the assembly
    /// finishes and again in the run summary), whichever is larger.</summary>
    /// <param name="log">The step log's text.</param>
    /// <returns>The count, or null when the log has no failed count.</returns>
    public static int? SummarizedFailures(string log)
    {
        ArgumentNullException.ThrowIfNull(log);
        try
        {
            var counts = FailedCountLine.Matches(log).Select(m => int.Parse(m.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            var assemblies = AssemblyFailureLine.Matches(log)
                .GroupBy(m => m.Groups["dll"].Value, StringComparer.Ordinal)
                .Select(g => g.Max(m => int.Parse(m.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture)))
                .ToList();
            return counts.Count == 0 && assemblies.Count == 0 ? null : Math.Max(counts.Sum(), assemblies.Sum());
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    /// <summary>How many "failed &lt;name&gt; (&lt;duration&gt;)" lines the log has, repeats included.</summary>
    /// <param name="log">The step log's text.</param>
    /// <returns>The count.</returns>
    public static int FailedLines(string log)
    {
        ArgumentNullException.ThrowIfNull(log);
        return log.Split('\n', StringSplitOptions.TrimEntries).Count(line => Match(line) is not null);
    }

    /// <summary>Whether an MTP run summary says no test ran: it prints "total: 0" (and "Zero tests ran").</summary>
    /// <param name="output">The run's output.</param>
    /// <returns>True when the output has totals and they add up to zero.</returns>
    public static bool RanNone(string output) => Totals(output) is { Count: > 0 } totals && totals.Sum() == 0;

    /// <summary>Whether an MTP run summary says some test ran.</summary>
    /// <param name="output">The run's output.</param>
    /// <returns>True when the totals add up to more than zero.</returns>
    public static bool Ran(string output) => Totals(output).Sum() > 0;

    /// <summary>Whether a test filter names a test: equal to it or to its name without arguments, or a run of whole dot-separated segments of it (a method, Class.Method, a class). Never a plain substring.</summary>
    /// <param name="filter">The filter (a method name, Class.Method, a display name or a known-flake entry).</param>
    /// <param name="name">The test's name.</param>
    /// <returns>True when the filter names the test.</returns>
    public static bool Matches(string filter, string name)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(name);
        var wanted = filter.Trim();
        if (wanted.Length == 0)
        {
            return false;
        }

        if (string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase) || name.EndsWith("." + wanted, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var open = name.IndexOf('(', StringComparison.Ordinal);
        var segments = (open > 0 ? name[..open] : name).Split('.');
        var run = wanted.Split('.');
        return Enumerable.Range(0, Math.Max(0, segments.Length - run.Length + 1))
            .Any(start => segments.Skip(start).Take(run.Length).SequenceEqual(run, StringComparer.OrdinalIgnoreCase));
    }

    private static List<long> Totals(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        try
        {
            return [.. TotalLine.Matches(output).Select(m => long.Parse(m.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture))];
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }
    }

    private static string? Match(string line)
    {
        try
        {
            var match = FailedLine.Match(line);
            if (!match.Success)
            {
                match = BashSuiteLine.Match(line);
            }

            return match.Success ? match.Groups["name"].Value : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^failed (?<name>\S.*?) \((\d+(\.\d+)?\s?(ms|s|m|h|d)\s?)+\)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex FailedLine { get; }

    // The bash runner (scripts/ci/bash-tests.sh): "FAIL  par  <suite>  0s  exit 1" per suite as it finishes, and "FAIL  par  <suite>  (exit 1, 0s)" in the summary block.
    [GeneratedRegex(@"^FAIL\s+(par|ser)\s+(?<name>\S+)\s+(\d+s\s+exit \d+|\(exit \d+, \d+s\))$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex BashSuiteLine { get; }

    [GeneratedRegex(@"^\s*failed: (?<n>\d{1,9})\s*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Multiline, matchTimeoutMilliseconds: 100)]
    private static partial Regex FailedCountLine { get; }

    [GeneratedRegex(@"^\s*(?<dll>\S.*?\.dll) \([^)\r\n]*\) failed with (?<n>\d{1,9}) error\(s\)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Multiline, matchTimeoutMilliseconds: 100)]
    private static partial Regex AssemblyFailureLine { get; }

    [GeneratedRegex(@"^\s*total: (?<n>\d{1,12})\s*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Multiline, matchTimeoutMilliseconds: 100)]
    private static partial Regex TotalLine { get; }
}
