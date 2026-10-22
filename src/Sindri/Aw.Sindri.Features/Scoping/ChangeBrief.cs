using System.Text;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The brief's text for a change (scoping spec §4): the PR body or, for a branch, its first commit message, then the commits, the changed files, the denied paths whose hunks were dropped, and the diff. Untrusted text, which the Step scrubs.</summary>
internal static class ChangeBrief
{
    /// <summary>The text, or an empty string when the change has no PR body, no commit and no diff.</summary>
    /// <param name="change">The change.</param>
    /// <returns>The text.</returns>
    internal static string Compose(ChangeRecord change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var opening = Opening(change);
        if (string.IsNullOrWhiteSpace(opening) && change.Files.Count == 0 && change.Denied.Count == 0 && string.IsNullOrWhiteSpace(change.Diff))
        {
            return string.Empty;
        }

        var text = new StringBuilder().Append("# ").AppendLine(Title(change)).AppendLine().AppendLine(opening);
        List(text, "## Commits", change.Commits.Select(static m => m.Trim()));
        List(text, "## Changed files", change.Files);
        List(text, "## Denied paths (hunks dropped)", change.Denied);
        if (!string.IsNullOrWhiteSpace(change.Diff))
        {
            _ = text.AppendLine().AppendLine("## Diff").AppendLine(change.Diff);
        }

        if (change.Truncated)
        {
            _ = text.AppendLine().AppendLine("[truncated: the diff was cut at the read's cap]");
        }

        return text.ToString();
    }

    /// <summary>The first line of the PR body or, with none, of the first commit message; the reference when there is neither.</summary>
    /// <param name="change">The change.</param>
    /// <returns>The title.</returns>
    internal static string Title(ChangeRecord change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var first = Opening(change).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.IsNullOrEmpty(first) ? "Change " + change.Ref : first;
    }

    private static string Opening(ChangeRecord change) =>
        !string.IsNullOrWhiteSpace(change.PullRequestBody) ? change.PullRequestBody.Trim() : change.Commits.Count > 0 ? change.Commits[0].Trim() : string.Empty;

    private static void List(StringBuilder text, string heading, IEnumerable<string> items)
    {
        var lines = items.ToList();
        if (lines.Count == 0)
        {
            return;
        }

        _ = text.AppendLine().AppendLine(heading);
        foreach (var line in lines)
        {
            _ = text.Append("- ").AppendLine(line);
        }
    }
}
