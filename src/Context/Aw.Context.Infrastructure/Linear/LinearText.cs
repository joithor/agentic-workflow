using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The raw text of a fetched Linear record (the engine sanitizes, scrubs and trust-tags it): up to 64 KB of UTF-8, never splitting a surrogate pair.</summary>
internal static partial class LinearText
{
    private const int MaxBytes = 64 * 1024;

    /// <summary>
    /// An issue: its description, its comments, its project, its relations, every attachment, and the images found in all three. The ingest
    /// sanitizer removes markdown and HTML images from the stored text, so their alt text and link are listed again as
    /// plain lines (owner q105, 2026-10-24).
    /// </summary>
    public static string Of(LinearIssue issue)
    {
        var comments = issue.Comments?.Nodes ?? [];
        var attachments = issue.Attachments?.Nodes ?? [];
        var images = ImagesIn(issue.Description).Concat(comments.SelectMany(c => ImagesIn(c.Body)))
            .Concat(attachments.Where(IsImage).Select(a => (Alt: a.Title ?? "image", a.Url)))
            .DistinctBy(i => i.Url, StringComparer.Ordinal);
        var head = issue.Description + Section("Comments", comments.Select(c => $"- {c.User?.Name ?? "unknown"} ({Day(c.CreatedAt)}): {c.Body}"), More(issue.Comments, "comments"));
        var relations = (issue.Relations?.Nodes ?? []).Select(r => $"- {r.Type} {Ref(r.RelatedIssue)}")
            .Concat((issue.InverseRelations?.Nodes ?? []).Select(r => $"- (from) {r.Type} {Ref(r.Issue)}"));
        var tail = (issue.Project?.Name is { Length: > 0 } project ? $"\n\nProject: {project}" : "")
            + Section("Relations", relations, more: null)
            + Section("Attachments", attachments.Select(a => $"- {a.Title ?? Host(a.Url)} ({a.SourceType ?? "link"}) {a.Url}"), More(issue.Attachments, "attachments"))
            + Section("Images", images.Select(i => $"- {i.Alt} {i.Url}"), more: null);

        // The attachments and images are what the owner wants to survive: the 64 KB goes to the description, then the
        // comments, and the sections keep what is left whole. Only sections that alone pass 64 KB are cut, at a line.
        var tailBytes = Encoding.UTF8.GetByteCount(tail);
        return tailBytes > MaxBytes ? CutLines(tail) : Cut(head, MaxBytes - tailBytes) + tail;
    }

    /// <summary>A Linear Document: its content.</summary>
    public static string Of(LinearDocument document) => Cut(document.Content ?? "");

    /// <summary>A project: its description, a line each for state, lead, teams and dates, and its latest updates (newest first).</summary>
    public static string Of(LinearProject project, IReadOnlyList<LinearProjectUpdate> updates)
    {
        ArgumentNullException.ThrowIfNull(updates);
        return Cut((project.Description ?? "") + "\n\n" + string.Join('\n', new[]
        {
            Line("State", project.State ?? "unknown"),
            Line("Lead", project.Lead?.Name),
            Line("Teams", string.Join(", ", (project.Teams?.Nodes ?? []).Select(t => t.Key))),
            Line("Start", project.StartDate),
            Line("Target", project.TargetDate),
        }.Where(l => l.Length > 0)) + Section("Updates", updates.Select(u => $"- {u.Health ?? "unknown"} ({Day(u.CreatedAt)}): {u.Body}"), more: null));
    }

    /// <summary>A project update: its health, then its body.</summary>
    public static string Of(LinearProjectUpdate update) => Cut($"Health: {update.Health ?? "unknown"}\n\n{update.Body}");

    /// <summary>A day as 'yyyy-MM-dd' (UTC).</summary>
    public static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Line(string label, string? value) => string.IsNullOrEmpty(value) ? "" : $"{label}: {value}";

    private static string Section(string title, IEnumerable<string> lines, string? more) =>
        lines.ToList() is { Count: > 0 } kept ? $"\n\n{title}:\n" + string.Join('\n', kept) + (more is null ? "" : "\n" + more) : "";

    // The mark a list leaves when Linear has more than the first page of it.
    private static string? More<T>(LinearConnection<T>? connection, string what) =>
        connection?.PageInfo is { HasNextPage: true } ? $"(more {what} not shown)" : null;

    private static string Ref(LinearIssueRef? issue) => issue is null ? "unknown" : $"{issue.Identifier} {issue.Title}".TrimEnd();

    private static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.Length > 0 ? uri.Host : url;

    private static bool IsImage(LinearAttachment attachment) =>
        Uri.TryCreate(attachment.Url, UriKind.Absolute, out var uri)
        && (string.Equals(uri.Host, "uploads.linear.app", StringComparison.OrdinalIgnoreCase)
            || ImageExtensions.Contains(Path.GetExtension(uri.AbsolutePath), StringComparer.OrdinalIgnoreCase));

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp"];

    private static IEnumerable<(string Alt, string Url)> ImagesIn(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        foreach (Match m in MarkdownImage.Matches(text))
        {
            yield return (Alt(m.Groups["alt"].Value), m.Groups["url"].Value);
        }

        foreach (Match tag in HtmlImage.Matches(text))
        {
            if (HtmlSrc.Match(tag.Value) is { Success: true } src)
            {
                yield return (HtmlAlt.Match(tag.Value) is { Success: true } alt ? Alt(alt.Groups["v"].Value) : "image", src.Groups["v"].Value);
            }
        }
    }

    private static string Alt(string alt) => alt.Trim() is { Length: > 0 } trimmed ? trimmed.ReplaceLineEndings(" ") : "image";

    [GeneratedRegex("!\\[(?<alt>[^\\]\\n]{0,500})\\]\\(\\s*<?(?<url>[^\\s)>]+)>?(?:\\s+\"[^\"\\n]*\")?\\s*\\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MarkdownImage { get; }

    [GeneratedRegex("<img\\b[^>]{0,4000}>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlImage { get; }

    [GeneratedRegex("\\bsrc\\s*=\\s*[\"'](?<v>[^\"']+)[\"']", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlSrc { get; }

    [GeneratedRegex("\\balt\\s*=\\s*[\"'](?<v>[^\"']*)[\"']", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlAlt { get; }

    private const string MoreMark = "\n(more not shown)";

    // Sections that alone pass the budget: whole lines up to it, then the mark.
    private static string CutLines(string text)
    {
        var kept = Cut(text, MaxBytes - Encoding.UTF8.GetByteCount(MoreMark));
        var lastLine = kept.LastIndexOf('\n');
        return (lastLine > 0 ? kept[..lastLine] : kept) + MoreMark;
    }

    private static string Cut(string text) => Cut(text, MaxBytes);

    private static string Cut(string text, int maxBytes)
    {
        var bytes = 0;
        var end = 0;
        while (end < text.Length)
        {
            var width = char.IsHighSurrogate(text[end]) && end + 1 < text.Length ? 2 : 1;
            var size = Encoding.UTF8.GetByteCount(text.AsSpan(end, width));
            if (bytes + size > maxBytes)
            {
                break;
            }

            bytes += size;
            end += width;
        }

        return end == text.Length ? text : text[..end];
    }
}
