using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Kernel;

namespace Aw.Context.Core.Lenses;

/// <summary>Builds the file map of a directory (spec §8.9, S-4). Pure over an applied graph and a file listing.</summary>
public static class FileMapBuilder
{
    private const int MaxFeatures = 10;
    private const string CodePrefix = "code:";

    /// <summary>Builds the view.</summary>
    /// <param name="map">The map read ('v13@7c3e1a2').</param>
    /// <param name="query">What was asked.</param>
    /// <param name="files">Every tracked file of the repo, paths from the root.</param>
    /// <param name="graph">The graph a caller may see: overrides applied.</param>
    /// <returns>The view, or SND-CONTEXT-003 when no file lies under the path.</returns>
    public static Result<FileMapView> Build(string map, FileMapQuery query, IReadOnlyList<string> files, GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(graph);
        var path = query.Path.Length == 0 || query.Path.EndsWith('/') ? query.Path : query.Path + "/";
        var under = files.Where(f => f.StartsWith(path, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (path.Length > 0 && under.Count == 0)
        {
            return ContextErrors.UnknownAnchor.With($"no file lies under '{(path.Length <= 80 ? path : "(a long path)")}' in {query.Repo.Value} (fix: list the parent directory)");
        }

        var edges = ImplementsByFile(graph);
        var status = under.ToDictionary(f => f, f => StatusOf(edges, f), StringComparer.Ordinal);
        var entries = Children(path, under, status, edges);
        var suggested = under.SelectMany(f => Unconfirmed(edges, f)).OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal)
            .Select(e => new FileMapSuggestion(LinkIds.For(e), e.From, e.To, e.Confidence));
        var totals = new FileMapTotals(under.Count(f => status[f] == FileMapStatus.Mapped), under.Count(f => status[f] == FileMapStatus.Suggested), under.Count(f => status[f] == FileMapStatus.Unmapped));
        return new FileMapView(map, query.Draft, query.Repo, path, [.. query.UnmappedOnly ? entries.Where(e => e.Status != FileMapStatus.Mapped) : entries], [.. suggested], totals,
            new LensSymbols("unavailable", "index not built"));
    }

    private static List<FileMapEntry> Children(string path, List<string> under, Dictionary<string, FileMapStatus> status, Dictionary<string, List<GraphEdge>> edges) =>
        [.. under.GroupBy(f => Child(path, f), StringComparer.Ordinal).Select(g =>
            {
                var directory = g.Key.EndsWith('/');
                var features = g.SelectMany(f => edges.GetValueOrDefault(f) ?? []).Select(e => e.To).DistinctBy(k => k.Value, StringComparer.Ordinal)
                    .OrderBy(k => k.Value, StringComparer.Ordinal).Take(MaxFeatures).ToList();
                var worst = g.Max(f => status[f]);
                return new FileMapEntry(g.Key.TrimEnd('/')[(g.Key.TrimEnd('/').LastIndexOf('/') + 1)..], g.Key, directory, worst, g.Count(), features);
            }).OrderByDescending(e => e.Directory).ThenBy(e => e.Name, StringComparer.Ordinal)];

    // The direct child of a directory that holds a file: 'src/tabs/' for 'src/tabs/a/b.ts' under 'src/', the file itself when it sits right there.
    private static string Child(string path, string file)
    {
        var rest = file[path.Length..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        return slash < 0 ? file : path + rest[..(slash + 1)];
    }

    private static Dictionary<string, List<GraphEdge>> ImplementsByFile(GraphSnapshot graph) =>
        graph.Edges.Where(e => e.Type == EdgeType.Implements && e.From.Value.StartsWith(CodePrefix, StringComparison.Ordinal) && !e.From.Value.Contains('#', StringComparison.Ordinal))
            .GroupBy(e => e.From.Value[CodePrefix.Length..], StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

    private static FileMapStatus StatusOf(Dictionary<string, List<GraphEdge>> edges, string file)
    {
        var own = edges.GetValueOrDefault(file) ?? [];
        return own.Exists(e => Confirmation.Of(e.Provenance)) ? FileMapStatus.Mapped : own.Count > 0 ? FileMapStatus.Suggested : FileMapStatus.Unmapped;
    }

    private static IEnumerable<GraphEdge> Unconfirmed(Dictionary<string, List<GraphEdge>> edges, string file) =>
        (edges.GetValueOrDefault(file) ?? []).Where(e => !Confirmation.Of(e.Provenance));
}
