using System.Globalization;
using System.Text;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The raw text of a fetched Linear record (the engine sanitizes, scrubs and trust-tags it): up to 64 KB of UTF-8, never splitting a surrogate pair.</summary>
internal static class LinearText
{
    private const int MaxBytes = 64 * 1024;

    /// <summary>An issue: its description, its comments, and the pull requests attached to it.</summary>
    public static string Of(LinearIssue issue) =>
        Cut(issue.Description + Section("Comments", (issue.Comments?.Nodes ?? []).Select(c => $"- {c.User?.Name ?? "unknown"} ({Day(c.CreatedAt)}): {c.Body}"))
            + Section("Pull requests", (issue.Attachments?.Nodes ?? []).Where(IsPullRequest).Select(a => $"- {a.Title ?? "pull request"} {a.Url}")));

    /// <summary>A Linear Document: its content.</summary>
    public static string Of(LinearDocument document) => Cut(document.Content ?? "");

    /// <summary>A project: its description and a line each for state, lead, teams and dates.</summary>
    public static string Of(LinearProject project)
    {
        var lines = new List<string>
        {
            "State: " + (project.State ?? "unknown"),
            project.Lead?.Name is { } lead ? "Lead: " + lead : "",
            project.Teams?.Nodes is { Count: > 0 } teams ? "Teams: " + string.Join(", ", teams.Select(t => t.Key)) : "",
            project.StartDate is null && project.TargetDate is null ? "" : $"Start: {project.StartDate ?? "none"}, target: {project.TargetDate ?? "none"}",
        };
        return Cut((project.Description ?? "") + "\n\n" + string.Join('\n', lines.Where(l => l.Length > 0)));
    }

    /// <summary>A project update: its health, then its body.</summary>
    public static string Of(LinearProjectUpdate update) => Cut($"Health: {update.Health ?? "unknown"}\n\n{update.Body}");

    /// <summary>A day as 'yyyy-MM-dd' (UTC).</summary>
    public static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Section(string title, IEnumerable<string> lines) =>
        lines.ToList() is { Count: > 0 } kept ? $"\n\n{title}:\n" + string.Join('\n', kept) : "";

    private static bool IsPullRequest(LinearAttachment attachment) =>
        string.Equals(attachment.SourceType, "github", StringComparison.OrdinalIgnoreCase) && attachment.Url.Contains("/pull/", StringComparison.Ordinal);

    private static string Cut(string text)
    {
        var bytes = 0;
        var end = 0;
        while (end < text.Length)
        {
            var width = char.IsHighSurrogate(text[end]) && end + 1 < text.Length ? 2 : 1;
            var size = Encoding.UTF8.GetByteCount(text.AsSpan(end, width));
            if (bytes + size > MaxBytes)
            {
                break;
            }

            bytes += size;
            end += width;
        }

        return end == text.Length ? text : text[..end];
    }
}
