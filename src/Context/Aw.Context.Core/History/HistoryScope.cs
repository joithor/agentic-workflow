using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;

namespace Aw.Context.Core.History;

/// <summary>The anchors of a history read, resolved against one map snapshot: their own scopes, the stored intent reached from them and the files that implement them.</summary>
public sealed class HistoryScope
{
    internal HistoryScope(GraphSnapshot graph, List<LensScope> scopes, List<string> missing, List<LensRecord> records, List<LensDecision> decisions, List<HistoryFile> files)
    {
        Graph = graph;
        Scopes = scopes;
        MissingAnchors = missing;
        Records = records;
        Decisions = decisions;
        Files = files;
        AnchorKeys = [.. scopes.Select(s => s.Anchor.Key.Value)];
        Anchors = [.. scopes.Select(s => new LensAnchor(s.Anchor.Key, s.Anchor.Label, s.Anchor.Key.Kind, TrustPolicy.ForNode(s.Anchor, LensScope.Confirmed(s.Anchor.Provenance))))];
        Own = scopes.SelectMany(s => s.Own).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The anchors that resolved, in request order.</summary>
    public IReadOnlyList<LensAnchor> Anchors { get; }

    /// <summary>The values that named no node this caller may read, as given.</summary>
    public IReadOnlyList<string> MissingAnchors { get; }

    /// <summary>The resolved anchors' keys.</summary>
    public IReadOnlyList<string> AnchorKeys { get; }

    /// <summary>Every record that mentions an own-scope node, uncut, in key order, each with its anchors.</summary>
    public IReadOnlyList<LensRecord> Records { get; }

    /// <summary>Every decision that decides an own-scope node, uncut, in key order, each with its anchors.</summary>
    public IReadOnlyList<LensDecision> Decisions { get; }

    /// <summary>The files that implement own-scope nodes: confirmed first, then by confidence, then by key.</summary>
    public IReadOnlyList<HistoryFile> Files { get; }

    internal GraphSnapshot Graph { get; }

    internal IReadOnlyList<LensScope> Scopes { get; }

    internal HashSet<string> Own { get; }
}
