using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
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
    /// The one function from a stored snapshot to what a caller may see (review #220 run 3), which every reader uses. The overrides are applied first (a pending
    /// proposal only when <paramref name="pending"/>), then what the repos' deny globs hide is judged on that applied graph (<see cref="DenyCut"/>:
    /// the code nodes of denied paths, the nodes only a denied file evidences, the features only those files implement, their edges and denied evidence,
    /// and the overrides about them), then, for a session, only its repo's scope. The stored graph and the overrides are cut by the same decisions, so
    /// lenses, status, export, the review queue, promote and the history can't disagree.
    /// </summary>
    /// <param name="graph">The stored graph.</param>
    /// <param name="overrides">Every override.</param>
    /// <param name="scope">The profile's context section and repos, and the caller (run <see cref="Gate"/> first).</param>
    /// <param name="pending">True when a 'propose' row's node is part of the map (a draft); a current map never holds a pending proposal.</param>
    /// <param name="callerScope">False to apply the deny globs only, for a decision the engine doesn't scope to the caller's repo (a review, a proposal's status).</param>
    /// <returns>The visible view, or SND-CONTEXT-001 for an invalid section, or SND-CONTEXT-003 when a session's repo implements nothing in the map.</returns>
    public static Result<MapView> View(GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, ContextScope scope, bool pending, bool callerScope = true)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(overrides);
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

        var layer = pending ? overrides : [.. overrides.Where(o => o.Action != OverrideAction.Propose)];
        var applied = OverrideApplier.Apply(graph, layer).Graph;
        var cut = DenyCut.Of(graph, applied, repos.Value);
        var view = new MapView(cut.Apply(graph), cut.Apply(applied), cut.Apply(overrides), cut.Any);
        if (!callerScope || scope.Caller.Kind != ContextCallerKind.Session)
        {
            return view;
        }

        var scopedApplied = scope.Caller.Repo is { } repo ? view.Applied.ScopedTo(repo) : GraphSnapshot.Empty;
        if (scopedApplied.Nodes.Count == 0)
        {
            return ContextErrors.UnknownAnchor.With("no map nodes are implemented in this session's repo (X-Sindri-Repo)");
        }

        var scopedGraph = scope.Caller.Repo is { } own ? view.Graph.ScopedTo(own) : GraphSnapshot.Empty;
        return new MapView(scopedGraph, scopedApplied, [.. view.Overrides.Where(o => scopedGraph.Resolve(o.Target) is not null)], view.Filtered);
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

    /// <summary>The graph a build keeps: <see cref="View"/>'s deny cut over a built graph with no overrides (the globs are the profile's 'index.denyPaths').</summary>
    /// <param name="current">The graph.</param>
    /// <param name="built">The repos whose deny globs apply.</param>
    /// <returns>The graph itself when nothing is denied.</returns>
    public static GraphSnapshot WithoutDenied(GraphSnapshot current, IReadOnlyList<ContextRepo> built)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(built);
        return DenyCut.Of(current, current, built).Apply(current);
    }

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
