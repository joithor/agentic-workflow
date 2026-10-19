using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;

namespace Aw.Context.Core.Lenses;

/// <summary>Conflicts on the own scope (spec §8.9): a decision that supersedes another, once per decided node. 'contradicts' comes with S-CF.</summary>
internal static class LensConflicts
{
    private const int ShortKey = 17;

    /// <summary>The conflicts of the scope, ordered by node, decision and the one it supersedes.</summary>
    internal static List<LensConflict> Of(LensScope scope, IReadOnlyList<DecisionConflict> checkedPairs) => Of(scope.Graph, scope.Own, checkedPairs);

    /// <summary>The conflicts on the nodes of <paramref name="own"/> in <paramref name="graph"/>.</summary>
    internal static List<LensConflict> Of(GraphSnapshot graph, HashSet<string> own, IReadOnlyList<DecisionConflict> checkedPairs)
    {
        var supersedes = Supersedes(graph, own);
        var contradicts = Contradicts(graph, own, checkedPairs);
        return [.. supersedes, .. contradicts];
    }

    private static IEnumerable<GraphEdge> Into(GraphSnapshot graph, HashSet<string> own, EdgeType type) => graph.Edges.Where(e => e.Type == type && own.Contains(e.To.Value));

    private static List<LensConflict> Contradicts(GraphSnapshot graph, HashSet<string> own, IReadOnlyList<DecisionConflict> checkedPairs)
    {
        var decides = Into(graph, own, EdgeType.Decides).ToLookup(e => e.From.Value, e => e.To, StringComparer.Ordinal);
        return [.. checkedPairs.Where(c => c.Verdict == ConflictVerdict.Contradicts)
            .SelectMany(c => decides[c.A.Value].Where(n => decides[c.B.Value].Any(m => m.Value == n.Value)).Select(n => (Node: n, Pair: c)))
            .OrderBy(x => x.Node.Value, StringComparer.Ordinal).ThenBy(x => x.Pair.A.Value, StringComparer.Ordinal).ThenBy(x => x.Pair.B.Value, StringComparer.Ordinal)
            .Select(x => new LensConflict(ConflictKind.Contradicts, x.Node, x.Pair.A, x.Pair.B.Value, $"{Short(x.Pair.A)} contradicts {Short(x.Pair.B)}", [], []))];
    }

    private static List<LensConflict> Supersedes(GraphSnapshot graph, HashSet<string> own)
    {
        var supersedes = graph.Edges.Where(e => e.Type == EdgeType.Supersedes).ToList();
        return [.. Into(graph, own, EdgeType.Decides)
            .SelectMany(decides => supersedes.Where(s => s.From.Value == decides.From.Value).Select(s => (Node: decides.To, Decision: s.From, Other: s.To)))
            .Distinct().OrderBy(c => c.Node.Value, StringComparer.Ordinal).ThenBy(c => c.Decision.Value, StringComparer.Ordinal).ThenBy(c => c.Other.Value, StringComparer.Ordinal)
            .Select(c => new LensConflict(ConflictKind.Supersedes, c.Node, c.Decision, c.Other.Value, $"{Short(c.Decision)} supersedes {Short(c.Other)}", [], []))];
    }

    private static string Short(NodeKey key) => key.Value.Length <= ShortKey ? key.Value : key.Value[..ShortKey];
}
