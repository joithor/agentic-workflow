using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>
/// Who may read which part of the map (spec §3): humans and services see all of it; a session only while
/// 'context.sessions.enabled' is on (SND-CONTEXT-020), for a repo the profile maps, and only the nodes that repo implements,
/// with what lies between them. A repo that implements nothing answers SND-CONTEXT-003, like any unknown anchor.
/// </summary>
public static class MapAccess
{
    /// <summary>
    /// The part of <paramref name="graph"/> and its overrides the scope's caller may see (run <see cref="Gate"/> first): first without what the repos'
    /// deny globs hide (<see cref="WithoutDenied"/>, for every caller and every read, so export, status and the review queue agree with the lenses),
    /// then, for a session, only its repo's scope.
    /// </summary>
    /// <param name="graph">The stored graph.</param>
    /// <param name="overrides">Every override.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <returns>The visible graph and overrides, or SND-CONTEXT-001 for an invalid section, or SND-CONTEXT-003 when a session's repo implements nothing in the map.</returns>
    public static Result<(GraphSnapshot Graph, IReadOnlyList<MapOverride> Overrides)> Visible(
        GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, ContextScope scope)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(scope);
        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var repos = ScopeRules.Resolve(settings.Value, scope.Repos, scope.Caller);
        if (repos.IsError)
        {
            return repos.Error;
        }

        var (allowed, removed) = Denied(graph, repos.Value);
        var kept = removed.Count == 0 ? overrides : [.. overrides.Where(o => !removed.Contains(o.Target.Value) && (o.Other is null || !removed.Contains(o.Other.Value.Value)))];
        var caller = scope.Caller;
        if (caller.Kind != ContextCallerKind.Session)
        {
            return (allowed, kept);
        }

        var scoped = caller.Repo is { } repo ? allowed.ScopedTo(repo) : GraphSnapshot.Empty;
        if (scoped.Nodes.Count == 0)
        {
            return ContextErrors.UnknownAnchor.With("no map nodes are implemented in this session's repo (X-Sindri-Repo)");
        }

        return (scoped, [.. kept.Where(o => scoped.Resolve(o.Target) is not null)]);
    }

    /// <summary>The checks that need no map, the same ones every engine read makes: the context section is valid (001), and a session needs sessions on (020) and a repo the profile maps (003).</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <returns>Null when the caller may read, otherwise the error.</returns>
    public static Error? Gate(ContextScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var repos = ScopeRules.Resolve(settings.Value, scope.Repos, scope.Caller);
        return repos.IsError ? repos.Error : null;
    }

    /// <summary>
    /// Whether the profile maps the repo a map file describes (an import's check): a map for a repo the profile doesn't
    /// map would answer every session read with SND-CONTEXT-003.
    /// </summary>
    /// <param name="repo">The file's 'repo'.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <returns>Null when mapped, otherwise SND-CONTEXT-001 (an invalid section) or SND-CONTEXT-031 on 'repo'.</returns>
    public static Error? Maps(ContextRepoName repo, ContextScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var mapped = ScopeRules.Resolve(settings.Value, scope.Repos, scope.Caller with { Kind = ContextCallerKind.Human });
        return mapped.IsError || mapped.Value.Any(r => string.Equals(r.Name.Value, repo.Value, StringComparison.Ordinal))
            ? (mapped.IsError ? mapped.Error : null)
            : ContextErrors.MapFileInvalid.With(
                $"repo: '{repo.Value}' isn't a repo the approved profile maps ({string.Join(", ", mapped.Value.Select(r => r.Name.Value))}); nothing was written (fix: name one of them, or add the repo to the profile and approve it)");
    }

    /// <summary>
    /// The graph without the code nodes of denied paths, every edge touching them, the model-made features only those files implemented, and the
    /// deterministic or model nodes and edges whose only evidence is a denied file; denied evidence is struck from what stays. A deny glob added after the
    /// map was built keeps the path out of every read, before any rebuild (context spec §3, "Denied paths"; the globs are the profile's 'index.denyPaths').
    /// </summary>
    /// <param name="current">The graph.</param>
    /// <param name="built">The repos whose deny globs apply.</param>
    /// <returns>The graph itself when nothing is denied.</returns>
    public static GraphSnapshot WithoutDenied(GraphSnapshot current, IReadOnlyList<ContextRepo> built) => Denied(current, built).Graph;

    private static (GraphSnapshot Graph, HashSet<string> Removed) Denied(GraphSnapshot current, IReadOnlyList<ContextRepo> built)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(built);
        var denied = current.Nodes.Where(n => n.Key.Kind == NodeKind.Code && built.Any(r => r.Denies(n.Key.Value["code:".Length..]))).Select(n => n.Key.Value);
        var withEvidence = current.Nodes.Where(n => n.Provenance != Provenance.Human && OnlyDenied(n.EvidenceOrEmpty, built)).Select(n => n.Key.Value);
        var removed = denied.Concat(withEvidence).ToHashSet(StringComparer.Ordinal);
        if (removed.Count == 0 && !AnyDeniedEvidence(current, built))
        {
            return (current, removed);
        }

        var cut = current.Edges.Where(e => removed.Contains(e.From.Value) || removed.Contains(e.To.Value) || (e.Provenance != Provenance.Human && OnlyDenied(e.EvidenceOrEmpty, built))).ToHashSet();
        var orphaned = OrphanedFeatures(current, cut, removed);
        removed.UnionWith(orphaned);
        return (current with
        {
            Nodes = [.. current.Nodes.Where(n => !removed.Contains(n.Key.Value)).Select(n => n with { Evidence = Allowed(n.Evidence, built) })],
            Edges = [.. current.Edges.Where(e => !cut.Contains(e) && !orphaned.Contains(e.From.Value) && !orphaned.Contains(e.To.Value)).Select(e => e with { Evidence = Allowed(e.Evidence, built) })],
        }, removed);
    }

    private static bool AnyDeniedEvidence(GraphSnapshot graph, IReadOnlyList<ContextRepo> built) =>
        graph.Nodes.Any(n => HasDenied(n.EvidenceOrEmpty, built)) || graph.Edges.Any(e => HasDenied(e.EvidenceOrEmpty, built));

    // The model-made features left with no implementing file once the cut edges are gone.
    private static HashSet<string> OrphanedFeatures(GraphSnapshot current, HashSet<GraphEdge> cut, HashSet<string> removed)
    {
        var features = current.Nodes.Where(n => n.Key.Kind == NodeKind.Feature && n.Provenance == Provenance.Model).Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var stillImplemented = current.Edges.Where(e => e.Type == EdgeType.Implements && !cut.Contains(e)).Select(e => e.To.Value).ToHashSet(StringComparer.Ordinal);
        return current.Edges.Where(e => e.Type == EdgeType.Implements && cut.Contains(e)).Select(e => e.To.Value)
            .Where(feature => features.Contains(feature) && !removed.Contains(feature) && !stillImplemented.Contains(feature)).ToHashSet(StringComparer.Ordinal);
    }

    private static bool Denies(EvidenceRef reference, IReadOnlyList<ContextRepo> built)
    {
        var key = reference.Key.Value;
        return built.Any(r => key.StartsWith($"repo:{r.Name.Value}/", StringComparison.Ordinal) && r.Denies(key[$"repo:{r.Name.Value}/".Length..]));
    }

    private static bool HasDenied(IReadOnlyList<EvidenceRef> evidence, IReadOnlyList<ContextRepo> built) => evidence.Any(r => Denies(r, built));

    private static bool OnlyDenied(IReadOnlyList<EvidenceRef> evidence, IReadOnlyList<ContextRepo> built) => evidence.Count > 0 && evidence.All(r => Denies(r, built));

    private static IReadOnlyList<EvidenceRef>? Allowed(IReadOnlyList<EvidenceRef>? evidence, IReadOnlyList<ContextRepo> built) =>
        evidence is null || !HasDenied(evidence, built) ? evidence : [.. evidence.Where(r => !Denies(r, built))];

    /// <summary>The export document for a version's visible graph: decision nodes and 'quotes' edges are left to the decisions, which imply them.</summary>
    /// <param name="version">The version.</param>
    /// <param name="graph">The visible graph.</param>
    /// <param name="overrides">The visible overrides.</param>
    /// <param name="exportedAt">The time now.</param>
    /// <returns>The document, with orphaned overrides listed.</returns>
    public static MapDocument Export(MapVersion version, GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, DateTimeOffset exportedAt)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(graph);
        var orphaned = OverrideApplier.Apply(graph, overrides).Orphaned;
        var o = StringComparer.Ordinal;
        return new MapDocument(
            MapDocumentValidator.SchemaVersions[^1], MapDocumentKind.Export, version.Repo,
            [.. graph.Nodes.Where(n => n.Key.Kind != NodeKind.Decision).OrderBy(n => n.Key.Value, o)],
            [.. graph.Edges.Where(e => e.Type != EdgeType.Quotes).OrderBy(e => e.From.Value, o).ThenBy(e => e.To.Value, o).ThenBy(e => e.Type).ThenBy(e => e.Order)],
            [.. graph.Aliases.OrderBy(a => a.Alias.Value, o)],
            [.. graph.Decisions.OrderBy(d => d.ToKey().Value, o)],
            [.. overrides.OrderBy(x => x.At).ThenBy(x => x.ToId().Value, o)],
            version.KeyVersion, version.Commit, version.Number, exportedAt,
            [.. orphaned.Select(x => x.Target).DistinctBy(k => k.Value, o).OrderBy(k => k.Value, o)]);
    }
}
