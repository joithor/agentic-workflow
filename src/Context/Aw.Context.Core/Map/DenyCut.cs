using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Map;

/// <summary>
/// What the repos' deny globs take out of a map (context spec §3, "Denied paths"), decided once, from the stored nodes and from the nodes the overrides add
/// (a pending proposal), never from a graph an override has already changed: a node a person rejected, deleted or merged away is still judged. It takes the code nodes
/// of denied paths, the record nodes of denied repo documents and the decisions quoting them, the nodes whose only evidence is a denied file (model-made or
/// deterministic in the stored graph, whatever a person's accept made of them), the model features only those files implemented, and every edge touching them. Denied evidence
/// is struck from what stays. The same decision then cuts the stored graph and the overrides (<see cref="MapAccess.View"/> applies the rest to the cut graph), so no reader
/// sees a graph the overrides were applied to before the cut.
/// </summary>
internal sealed class DenyCut
{
    private readonly IReadOnlyList<ContextRepo> _repos;
    private readonly HashSet<string> _removed;
    private readonly HashSet<string> _cutLinks;
    private readonly Dictionary<string, List<GraphNode>> _origin;
    private readonly Dictionary<string, string> _aliases;

    private DenyCut(IReadOnlyList<ContextRepo> repos, HashSet<string> removed, HashSet<string> cutLinks, Dictionary<string, List<GraphNode>> origin, Dictionary<string, string> aliases, bool any)
    {
        _repos = repos;
        _removed = removed;
        _cutLinks = cutLinks;
        _origin = origin;
        _aliases = aliases;
        Any = any;
    }

    /// <summary>Whether the globs take or strike anything from the graph this was judged on.</summary>
    public bool Any { get; }

    /// <summary>Judges the stored graph and the overrides together.</summary>
    /// <param name="stored">The graph as stored, before overrides.</param>
    /// <param name="overrides">Every override, a pending proposal too: what they add or remap is judged, so the decision does not depend on which reader asks.</param>
    /// <param name="repos">The repos whose deny globs apply.</param>
    /// <returns>The cut.</returns>
    public static DenyCut Of(GraphSnapshot stored, IReadOnlyList<MapOverride> overrides, IReadOnlyList<ContextRepo> repos)
    {
        var applied = overrides.Count == 0 ? stored : OverrideApplier.Apply(stored, overrides).Graph;
        var deniedDecisions = stored.Decisions.Where(d => DeniesKey(d.Record.Value, repos)).Select(d => d.ToKey().Value).ToHashSet(StringComparer.Ordinal);
        var origin = Origin(stored, overrides);
        var removed = origin.Where(p => IsCut(p.Value[0], deniedDecisions, repos)).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        removed.UnionWith(applied.Nodes.Where(n => IsDeniedKey(n.Key, repos)).Select(n => n.Key.Value));
        var edges = ReferenceEquals(applied, stored) ? stored.Edges : [.. stored.Edges, .. applied.Edges];
        removed.UnionWith(OrphanedFeatures(origin, stored, [.. edges.Where(e => IsCut(e, removed, repos))], removed));
        var cutLinks = edges.Where(e => IsCut(e, removed, repos)).Select(e => LinkIds.For(e).Value).ToHashSet(StringComparer.Ordinal);
        var any = removed.Count > 0 || cutLinks.Count > 0 || deniedDecisions.Count > 0
            || origin.Values.Any(nodes => nodes.Exists(n => HasDenied(n.EvidenceOrEmpty, repos))) || stored.Edges.Any(e => HasDenied(e.EvidenceOrEmpty, repos));
        return new DenyCut(repos, removed, cutLinks, origin, stored.Aliases.ToDictionary(a => a.Alias.Value, a => a.Target.Value, StringComparer.Ordinal), any);
    }

    /// <summary>The evidence a deny glob leaves.</summary>
    /// <param name="evidence">The evidence, or null.</param>
    /// <returns>The same list when no glob denies any of it.</returns>
    public IReadOnlyList<EvidenceRef>? AllowedEvidence(IReadOnlyList<EvidenceRef>? evidence) => Allowed(evidence, _repos);

    /// <summary>The graph without what was cut: the nodes, the edges (by their endpoints or link), the aliases and decisions about them, and denied evidence struck from the rest.</summary>
    /// <param name="graph">The stored graph.</param>
    /// <returns>The graph itself when nothing is denied.</returns>
    public GraphSnapshot Apply(GraphSnapshot graph)
    {
        if (!Any)
        {
            return graph;
        }

        return graph with
        {
            Nodes = [.. graph.Nodes.Where(n => !_removed.Contains(n.Key.Value)).Select(n => n with { Evidence = Allowed(n.Evidence, _repos) })],
            Edges = [.. graph.Edges.Where(e => !_removed.Contains(e.From.Value) && !_removed.Contains(e.To.Value) && !_cutLinks.Contains(LinkIds.For(e).Value)).Select(e => e with { Evidence = Allowed(e.Evidence, _repos) })],
            Aliases = [.. graph.Aliases.Where(a => !_removed.Contains(a.Alias.Value) && !_removed.Contains(a.Target.Value) && !IsDeniedKey(a.Alias, _repos) && !IsDeniedKey(a.Target, _repos))],
            Decisions = [.. graph.Decisions.Where(d => !DeniesKey(d.Record.Value, _repos) && !_removed.Contains(d.ToKey().Value))],
        };
    }

    /// <summary>The overrides that still mean something: not about a removed node, a cut link or a denied path, a proposal without its denied evidence (or at all when that was all it had), and a reject that also names its proposal by the evidence the globs leave.</summary>
    /// <param name="overrides">Every override.</param>
    /// <returns>The overrides a caller may see.</returns>
    public IReadOnlyList<MapOverride> Apply(IReadOnlyList<MapOverride> overrides)
    {
        var kept = new List<MapOverride>(overrides.Count);
        foreach (var o in overrides)
        {
            if (About(o))
            {
                continue;
            }

            var row = o.Proposal is { Evidence: { Count: > 0 } evidence } payload && HasDenied(evidence, _repos) ? o with { Proposal = payload with { Evidence = Allowed(evidence, _repos) } } : o;
            kept.Add(row.Action == OverrideAction.Reject && row.Rejection is { } identity ? row with { AllowedRejection = AllowedIdentity(row.Target, identity) } : row);
        }

        return kept;
    }

    // Every form a node was first seen in: as stored, and as each active propose row would add it. A reject names its proposal by the evidence it had then.
    private static Dictionary<string, List<GraphNode>> Origin(GraphSnapshot stored, IReadOnlyList<MapOverride> overrides)
    {
        var origin = new Dictionary<string, List<GraphNode>>(StringComparer.Ordinal);
        foreach (var n in stored.Nodes.Concat(OverrideLog.Active(overrides).Where(o => o.Action == OverrideAction.Propose).Select(ProposalRows.NodeOf).OfType<GraphNode>()))
        {
            if (origin.TryGetValue(n.Key.Value, out var forms))
            {
                forms.Add(n);
            }
            else
            {
                origin[n.Key.Value] = [n];
            }
        }

        return origin;
    }

    // The identity a reject written before the glob (hashing the full evidence) has once the node's denied evidence is struck.
    private string? AllowedIdentity(NodeKey target, string identity)
    {
        var key = _aliases.TryGetValue(target.Value, out var resolved) ? resolved : target.Value;
        if (!_origin.TryGetValue(key, out var forms))
        {
            return null;
        }

        var match = forms.Find(n => HasDenied(n.EvidenceOrEmpty, _repos) && RejectionIdentity.For(n) == identity);
        return match is null ? null : RejectionIdentity.For(match with { Evidence = Allowed(match.Evidence, _repos) });
    }

    private bool About(MapOverride o) =>
        _removed.Contains(o.Target.Value) || IsDeniedKey(o.Target, _repos) || (o.Other is { } other && (_removed.Contains(other.Value) || IsDeniedKey(other, _repos)))
        || (o.Link is { } link && _cutLinks.Contains(link.Value))
        || (o.Proposal is { Evidence: { Count: > 0 } evidence } && OnlyDenied(evidence, _repos));

    // A node the globs take: a denied path's code node, a denied document's record node, a decision quoting one, or a node a model or a scan made on denied files alone
    // (a person's accept doesn't give the file back: this judges the node as it was first seen).
    private static bool IsCut(GraphNode n, HashSet<string> deniedDecisions, IReadOnlyList<ContextRepo> repos) =>
        IsDeniedKey(n.Key, repos) || deniedDecisions.Contains(n.Key.Value) || (n.Provenance != Provenance.Human && OnlyDenied(n.EvidenceOrEmpty, repos));

    private static bool IsCut(GraphEdge e, HashSet<string> removed, IReadOnlyList<ContextRepo> repos) =>
        removed.Contains(e.From.Value) || removed.Contains(e.To.Value) || (e.Provenance != Provenance.Human && OnlyDenied(e.EvidenceOrEmpty, repos));

    private static bool IsDeniedKey(NodeKey key, IReadOnlyList<ContextRepo> repos) =>
        (key.Kind == NodeKind.Code && repos.Any(r => r.Denies(key.Value["code:".Length..]))) || (key.Kind == NodeKind.Record && DeniesKey(key.Value["record:".Length..], repos));

    // The model-made features left with no implementing file once the cut edges are gone, accepted or not: a person accepting a model feature doesn't give its denied file back.
    // Only stored implements edges count (a merge's remapped edge goes with the merge, which is dropped when its node is cut). A feature a person wrote stays.
    private static HashSet<string> OrphanedFeatures(Dictionary<string, List<GraphNode>> origin, GraphSnapshot stored, List<GraphEdge> cut, HashSet<string> removed)
    {
        var cutSet = cut.ToHashSet();
        var stillImplemented = stored.Edges.Where(e => e.Type == EdgeType.Implements && !cutSet.Contains(e)).Select(e => e.To.Value).ToHashSet(StringComparer.Ordinal);
        return cut.Where(e => e.Type == EdgeType.Implements).Select(e => e.To.Value)
            .Where(feature => origin.TryGetValue(feature, out var forms) && forms[0] is { Provenance: Provenance.Model, Key.Kind: NodeKind.Feature } && !removed.Contains(feature) && !stillImplemented.Contains(feature))
            .ToHashSet(StringComparer.Ordinal);
    }

    // A record key such as 'repo:work-app/private/notes.md' is denied when the repo it names denies the path after the repo.
    private static bool DeniesKey(string recordKey, IReadOnlyList<ContextRepo> repos) =>
        repos.Any(r => recordKey.StartsWith($"repo:{r.Name.Value}/", StringComparison.Ordinal) && r.Denies(recordKey[$"repo:{r.Name.Value}/".Length..]));

    private static bool Denies(EvidenceRef reference, IReadOnlyList<ContextRepo> repos) => DeniesKey(reference.Key.Value, repos);

    private static bool HasDenied(IReadOnlyList<EvidenceRef> evidence, IReadOnlyList<ContextRepo> repos) => evidence.Any(r => Denies(r, repos));

    private static bool OnlyDenied(IReadOnlyList<EvidenceRef> evidence, IReadOnlyList<ContextRepo> repos) => evidence.Count > 0 && evidence.All(r => Denies(r, repos));

    private static IReadOnlyList<EvidenceRef>? Allowed(IReadOnlyList<EvidenceRef>? evidence, IReadOnlyList<ContextRepo> repos) =>
        evidence is null || !HasDenied(evidence, repos) ? evidence : [.. evidence.Where(r => !Denies(r, repos))];
}
