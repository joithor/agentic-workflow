using System.Text.RegularExpressions;

namespace Aw.Meta.Core.Gate;

/// <summary>Reads the failing tests out of a Microsoft.Testing.Platform log and matches test names against filters.</summary>
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
            return match.Success ? match.Groups["name"].Value : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^failed (?<name>\S.*?) \((\d+(\.\d+)?\s?(ms|s|m|h|d)\s?)+\)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex FailedLine { get; }

    [GeneratedRegex(@"^\s*total: (?<n>\d{1,12})\s*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Multiline, matchTimeoutMilliseconds: 100)]
    private static partial Regex TotalLine { get; }
}
