using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>
/// Who may read which part of the map (spec §3): humans and services see all of it; a session only while
/// 'context.sessions.enabled' is on (SND-CONTEXT-020), and only the nodes its repo implements, with what lies between
/// them. A repo that implements nothing answers SND-CONTEXT-003, like any unknown anchor.
/// </summary>
public static class MapAccess
{
    /// <summary>The part of <paramref name="graph"/> and its overrides <paramref name="reader"/> may see.</summary>
    /// <param name="graph">The stored graph.</param>
    /// <param name="overrides">Every override.</param>
    /// <param name="reader">The caller and settings.</param>
    /// <returns>The visible graph and overrides, or SND-CONTEXT-003/020.</returns>
    public static Result<(GraphSnapshot Graph, IReadOnlyList<MapOverride> Overrides)> Visible(
        GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, MapReader reader)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(reader);
        if (reader.Caller.Kind != ContextCallerKind.Session)
        {
            return (graph, overrides);
        }

        if (Gate(reader) is { } refused)
        {
            return refused;
        }

        var repo = reader.Caller.Repo!;   // why: Gate refuses a session without a repo
        var scoped = graph.ScopedTo(repo);
        if (scoped.Nodes.Count == 0)
        {
            return ContextErrors.UnknownAnchor.With($"nothing in the map for repo '{repo}' (fix: context map status)");
        }

        return (scoped, [.. overrides.Where(o => scoped.Resolve(o.Target) is not null)]);
    }

    /// <summary>The checks that need no map: a session needs sessions on and a repo.</summary>
    /// <param name="reader">The caller and settings.</param>
    /// <returns>Null when the caller may read, otherwise SND-CONTEXT-020 or SND-CONTEXT-003.</returns>
    public static Error? Gate(MapReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (reader.Caller.Kind != ContextCallerKind.Session)
        {
            return null;
        }

        if (!reader.Settings.SessionsEnabled)
        {
            return ContextErrors.SessionsDisabled.With("session reads of the map are off (fix: set context.sessions.enabled: true in the profile, then profile approve)");
        }

        return string.IsNullOrWhiteSpace(reader.Caller.Repo)
            ? ContextErrors.UnknownAnchor.With("a session read needs its repo (X-Sindri-Repo)")
            : null;
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
        return new MapDocument(
            MapDocumentValidator.SchemaVersions[^1], MapDocumentKind.Export, version.Repo,
            [.. graph.Nodes.Where(n => n.Key.Kind != NodeKind.Decision)],
            [.. graph.Edges.Where(e => e.Type != EdgeType.Quotes)],
            graph.Aliases, graph.Decisions, overrides, version.KeyVersion, version.Commit, version.Number, exportedAt,
            [.. orphaned.Select(o => o.Target).DistinctBy(k => k.Value, StringComparer.Ordinal)]);
    }
}
