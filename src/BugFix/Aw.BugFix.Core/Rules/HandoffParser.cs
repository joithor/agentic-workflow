using System.Globalization;
using System.Text.RegularExpressions;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.Kernel;

namespace Aw.BugFix.Core.Rules;

/// <summary>
/// Reads the handoff /rootCause --investigate-only writes: its "status:" line, the "## Hypotheses" table
/// (# | Hypothesis | Cause-site files | Likelihood | Result) and the "## Root Cause" section.
/// </summary>
public static partial class HandoffParser
{
    /// <summary>Parses a handoff.</summary>
    /// <param name="markdown">The handoff text.</param>
    /// <returns>The handoff, or AW-BUGFIX-006 naming what is missing or malformed.</returns>
    public static Result<Handoff> Parse(string markdown)
    {
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var status = lines.FirstOrDefault(static l => l.StartsWith("status:", StringComparison.Ordinal));
        if (status is null)
        {
            return BugFixErrors.HandoffRefused.With("handoff has no status: line");
        }

        var heading = Array.FindIndex(lines, static l => l.Trim() == "## Hypotheses");
        if (heading < 0)
        {
            return BugFixErrors.HandoffRefused.With("handoff has no ## Hypotheses section");
        }

        var hypotheses = new List<Hypothesis>();
        foreach (var row in TableRows(lines.Skip(heading + 1)).Skip(2))
        {
            var parsed = ParseRow(row);
            if (parsed is null)
            {
                return BugFixErrors.HandoffRefused.With($"malformed hypotheses row: {row.Trim()}");
            }

            hypotheses.Add(parsed);
        }

        return new Handoff(status["status:".Length..].Trim(), hypotheses, RootCause(lines));
    }

    private static List<string> TableRows(IEnumerable<string> lines)
    {
        var rows = new List<string>();
        foreach (var line in lines)
        {
            if (line.Trim().StartsWith('|'))
            {
                rows.Add(line);
            }
            else if (rows.Count > 0 || line.Trim().Length > 0)
            {
                break;
            }
        }

        return rows;
    }

    private static Hypothesis? ParseRow(string row)
    {
        var cells = row.Trim().Trim('|').Split('|').Select(static c => c.Trim()).ToArray();
        if (cells.Length != 5 || !int.TryParse(cells[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        HypothesisResult? result = cells[4] switch
        {
            "confirmed" => HypothesisResult.Confirmed,
            "ruled-out" => HypothesisResult.RuledOut,
            "untested" => HypothesisResult.Untested,
            _ => null,
        };
        return result is { } r ? new Hypothesis(number, cells[1], Files(cells[2]), r) : null;
    }

    private static List<string> Files(string cell)
    {
        var ticked = Ticked.Matches(cell).Select(static m => m.Groups["file"].Value).ToList();
        return ticked.Count > 0
            ? ticked
            : [.. cell.Split(',').Select(static f => f.Trim()).Where(static f => f.Length > 0)];
    }

    private static string RootCause(string[] lines)
    {
        var start = Array.FindIndex(lines, static l => l.TrimEnd() == "## Root Cause");
        if (start < 0)
        {
            return string.Empty;
        }

        var body = lines.Skip(start + 1).TakeWhile(static l => !l.StartsWith("## ", StringComparison.Ordinal));
        return string.Join('\n', body).Trim();
    }

    [GeneratedRegex("`(?<file>[^`]+)`", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Ticked { get; }
}
