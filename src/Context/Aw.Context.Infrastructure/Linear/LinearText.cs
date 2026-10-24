using System.Globalization;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The raw text of a fetched Linear record (the engine sanitizes, scrubs and trust-tags it): whole, never cut.</summary>
internal static class LinearText
{
    /// <summary>An issue: its description, its comments, and the pull requests attached to it.</summary>
    public static string Of(LinearIssue issue) =>
        issue.Description + Section("Comments", (issue.Comments?.Nodes ?? []).Select(c => $"- {c.User?.Name ?? "unknown"} ({Day(c.CreatedAt)}): {c.Body}"), More(issue.Comments, "comments"))
            + Section("Pull requests", (issue.Attachments?.Nodes ?? []).Where(IsPullRequest).Select(a => $"- {a.Title ?? "pull request"} {a.Url}"), More(issue.Attachments, "attachments"));

    /// <summary>A Linear Document: its content.</summary>
    public static string Of(LinearDocument document) => document.Content ?? "";

    /// <summary>A project: its description, a line each for state, lead, teams and dates, and its latest updates (newest first).</summary>
    public static string Of(LinearProject project, IReadOnlyList<LinearProjectUpdate> updates)
    {
        ArgumentNullException.ThrowIfNull(updates);
        return (project.Description ?? "") + "\n\n" + string.Join('\n', new[]
        {
            Line("State", project.State ?? "unknown"),
            Line("Lead", project.Lead?.Name),
            Line("Teams", string.Join(", ", (project.Teams?.Nodes ?? []).Select(t => t.Key))),
            Line("Start", project.StartDate),
            Line("Target", project.TargetDate),
        }.Where(l => l.Length > 0)) + Section("Updates", updates.Select(u => $"- {u.Health ?? "unknown"} ({Day(u.CreatedAt)}): {u.Body}"), more: null);
    }

    /// <summary>A project update: its health, then its body.</summary>
    public static string Of(LinearProjectUpdate update) => $"Health: {update.Health ?? "unknown"}\n\n{update.Body}";

    /// <summary>A day as 'yyyy-MM-dd' (UTC).</summary>
    public static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Line(string label, string? value) => string.IsNullOrEmpty(value) ? "" : $"{label}: {value}";

    private static string Section(string title, IEnumerable<string> lines, string? more) =>
        lines.ToList() is { Count: > 0 } kept ? $"\n\n{title}:\n" + string.Join('\n', kept) + (more is null ? "" : "\n" + more) : "";

    // The mark a list leaves when Linear has more than the first page of it.
    private static string? More<T>(LinearConnection<T>? connection, string what) =>
        connection?.PageInfo is { HasNextPage: true } ? $"(more {what} not shown)" : null;

    private static bool IsPullRequest(LinearAttachment attachment) =>
        string.Equals(attachment.SourceType, "github", StringComparison.OrdinalIgnoreCase) && attachment.Url.Contains("/pull/", StringComparison.Ordinal);
}
