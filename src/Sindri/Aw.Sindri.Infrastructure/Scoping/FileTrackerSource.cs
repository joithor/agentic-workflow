using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Infrastructure.Scoping;

/// <summary>
/// The tracker port over an exported tracker directory (scoping spec §4, C8): <c language="text">issues.json</c> and <c language="text">projects.json</c> in the tracker's
/// GraphQL shape (<c language="text">data.issues.nodes</c>, <c language="text">data.projects.nodes</c>). An issue is found by its identifier, a project by its name, slug or id; a project's
/// children are the issues whose project is its id. Nothing in the directory is ever written.
/// </summary>
/// <param name="directory">The export's directory, or null when no tracker is configured.</param>
internal sealed class FileTrackerSource(string? directory) : ITrackerSource
{
    private const string Updated = "updatedAt";

    private static readonly string[] _projectKeys = ["name", "slugId", "id"];

    public async ValueTask<Result<TrackerRecord>> Read(string key, TrackerRead read, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(read);
        if (string.IsNullOrWhiteSpace(directory) || !File.Exists(Path.Combine(directory, "issues.json")))
        {
            return ScopingErrors.NoTrackerSource.With("no tracker export is configured (fix: set AW_SINDRI_TRACKER_DIR to a directory holding issues.json and projects.json)");
        }

        try
        {
            var issues = await Nodes("issues", cancellationToken).ConfigureAwait(false);
            var projects = await Nodes("projects", cancellationToken).ConfigureAwait(false);
            return Find(key, read, issues, projects);
        }
        catch (Exception e) when (e is JsonException or IOException or InvalidOperationException)
        {
            return SindriErrors.InvalidInput.With($"the tracker export in {directory} can't be read: {e.Message}");   // boundary: an export is a file a person put there
        }
    }

    private static Result<TrackerRecord> Find(string key, TrackerRead read, IReadOnlyList<JsonNode> issues, IReadOnlyList<JsonNode> projects)
    {
        if (issues.FirstOrDefault(i => string.Equals(Str(i, "identifier"), key, StringComparison.OrdinalIgnoreCase)) is { } issue)
        {
            var comments = Comments(issue, read);
            return new TrackerRecord(Str(issue, "identifier"), TrackerKind.Issue, Str(issue, "title"), Str(issue, "description"), Str(issue, Updated), comments, [], Count(issue) > read.CommentsPerItem);
        }

        var project = projects.FirstOrDefault(p => _projectKeys.Any(field => string.Equals(Str(p, field), key, StringComparison.OrdinalIgnoreCase)));
        return project is null
            ? SindriErrors.InvalidInput.With($"the tracker has no project or issue '{key}'")
            : Project(project, read, issues);
    }

    private static TrackerRecord Project(JsonNode project, TrackerRead read, IReadOnlyList<JsonNode> issues)
    {
        var inside = issues.Where(i => string.Equals(Str(i["project"], "id"), Str(project, "id"), StringComparison.Ordinal)).ToList();
        var children = inside.Take(read.MaxChildren).Select(i => new TrackerItem(Str(i, "identifier"), Str(i, "title"), Str(i, "description"), Comments(i, read))).ToList();
        var cut = inside.Count > read.MaxChildren || inside.Exists(i => Count(i) > read.CommentsPerItem);
        return new TrackerRecord(Str(project, "name"), TrackerKind.Project, Str(project, "name"), Str(project, "description"), Str(project, Updated), [], children, cut);
    }

    private static int Count(JsonNode issue) => issue["comments"]?["nodes"]?.AsArray().Count ?? 0;

    private static List<TrackerComment> Comments(JsonNode issue, TrackerRead read) =>
        [.. (issue["comments"]?["nodes"]?.AsArray() ?? []).Take(read.CommentsPerItem).Select(static c => new TrackerComment(Str(c?["user"], "name") is { Length: > 0 } name ? name : "unknown", Str(c, "body")))];

    private static string Str(JsonNode? node, string name) => node?[name]?.GetValue<string?>() ?? string.Empty;

    private async Task<IReadOnlyList<JsonNode>> Nodes(string name, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory!, name + ".json"); // why: Read checked the directory before calling
        if (!File.Exists(path))
        {
            return [];
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return [.. (JsonNode.Parse(text)?["data"]?[name]?["nodes"]?.AsArray() ?? []).OfType<JsonNode>()];
    }
}
