using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;

namespace Aw.Context.Core.Code;

/// <summary>The implementing files of the own scope, and the link between files and their tests.</summary>
internal static class CodeFiles
{
    internal static List<CodeFile> Of(CodeReach reach) =>
        [.. reach.Into(EdgeType.Implements).GroupBy(e => e.From.Value, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => File(reach, [.. g]))];

    /// <summary>Fills TestedBy of each file and Named of each edge test: a test covers the files that share an anchor with it.</summary>
    internal static (List<CodeFile> Files, List<CodeTest> Tests) Link(List<CodeFile> files, List<CodeTest> tests)
    {
        var linkedTests = tests.Select(t => t with { Named = [.. files.Where(f => Shares(f.Anchors, t.Anchors)).Select(f => f.Path)] }).ToList();
        var linkedFiles = files.Select(f => f with { TestedBy = [.. linkedTests.Where(t => Shares(f.Anchors, t.Anchors)).Select(t => t.Path)] }).ToList();
        return (linkedFiles, linkedTests);
    }

    internal static bool Shares(IReadOnlyList<NodeKey> left, IReadOnlyList<NodeKey> right) => left.Any(l => right.Any(r => r.Value == l.Value));

    /// <summary>The edge that speaks for a node: confirmed first, then confidence, then target.</summary>
    internal static GraphEdge Best(IEnumerable<GraphEdge> edges) =>
        edges.OrderByDescending(e => LensScope.Confirmed(e.Provenance)).ThenByDescending(e => e.Confidence).ThenBy(e => e.To.Value, StringComparer.Ordinal).First();

    internal static IReadOnlyList<NodeKey> Targets(IEnumerable<GraphEdge> edges) =>
        [.. edges.Select(e => e.To).DistinctBy(k => k.Value, StringComparer.Ordinal).OrderBy(k => k.Value, StringComparer.Ordinal)];

    private static CodeFile File(CodeReach reach, List<GraphEdge> implements)
    {
        var best = Best(implements);
        var node = reach.Nodes[best.From.Value];
        var confirmed = implements.Exists(e => LensScope.Confirmed(e.Provenance));
        var tests = reach.Graph.Edges.Where(e => e.Type == EdgeType.Tests && e.From.Value == best.From.Value && reach.Union.Contains(e.To.Value));
        return new CodeFile(
            best.From.Name,
            node.Label,
            NodeKind.Code,
            Targets(implements),
            Targets(tests),
            best.Provenance,
            best.Confidence,
            confirmed,
            Suggested: !confirmed,
            confirmed ? null : LinkIds.For(best),
            TrustPolicy.ForNode(node, confirmed),
            reach.AnchorsReaching(implements.Select(e => e.To.Value)),
            []);
    }
}
