using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;

namespace Aw.Context.Core.Code;

/// <summary>The test files the map links to the own scope with a 'tests' edge.</summary>
internal static class CodeTests
{
    internal static List<CodeTest> Of(CodeReach reach) =>
        [.. reach.Into(EdgeType.Tests).GroupBy(e => e.From.Value, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => Test(reach, [.. g]))];

    private static CodeTest Test(CodeReach reach, List<GraphEdge> edges)
    {
        var best = CodeFiles.Best(edges);
        var node = reach.Nodes[best.From.Value];
        var confirmed = edges.Exists(e => LensScope.Confirmed(e.Provenance));
        return new CodeTest(
            best.From.Name,
            node.Label,
            NodeKind.Code,
            CodeBasis.Edge,
            CodeFiles.Targets(edges),
            [],
            confirmed,
            Suggested: !confirmed,
            confirmed ? null : LinkIds.For(best),
            TrustPolicy.ForNode(node, confirmed),
            reach.AnchorsReaching(edges.Select(e => e.To.Value)));
    }
}
