using System.Globalization;
using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Host.Commands;

/// <summary>The text form of a worktree report, shared by worktree-list and worktree-sweep.</summary>
internal static class WorktreeText
{
    /// <summary>One line, "LABEL  path  branch=… pr=…", then a line per reason.</summary>
    internal static IEnumerable<string> Entry(WorktreeReport report, int width, string? label = null)
    {
        var state = label ?? (report.Decision == WorktreeDecision.Removable ? "REMOVABLE" : "KEPT");
        var pr = report.Pr is { } number ? string.Create(CultureInfo.InvariantCulture, $" pr=#{number}") + (report.PrState is { } s ? " " + s.ToString().ToUpperInvariant() : string.Empty) : string.Empty;
        yield return $"{state,-13} {report.Path.PadRight(width)}  {report.Branch ?? "(detached)"} {Short(report.Head)}{pr}";
        foreach (var reason in report.Reasons)
        {
            yield return "                - " + reason;
        }
    }

    private static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;
}
