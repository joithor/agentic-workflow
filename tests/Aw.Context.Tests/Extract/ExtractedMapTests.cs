using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Extract;

public sealed class ExtractedMapTests
{
    private static GraphNode Node(string key, string label, Provenance p = Provenance.Deterministic) => new(NodeKey.From(key), label, p);

    private static GraphEdge Edge(string from, string to) => new(NodeKey.From(from), NodeKey.From(to), EdgeType.Implements, Provenance.Deterministic);

    [Fact, Trait("Lane", "unit")]
    public void Plus_keeps_a_node_or_an_edge_both_hold_once_the_first_ones()
    {
        var one = new ExtractedMap([Node("feature:a", "First")], [Edge("code:a.ts", "feature:a")]);
        var two = new ExtractedMap([Node("feature:a", "Second"), Node("feature:b", "B")], [Edge("code:a.ts", "feature:a"), Edge("code:b.ts", "feature:b")]);

        var both = one.Plus(two);

        Assert.Equal([("feature:a", "First"), ("feature:b", "B")], both.Nodes.Select(n => (n.Key.Value, n.Label)));
        Assert.Equal(["code:a.ts", "code:b.ts"], both.Edges.Select(e => e.From.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Plus_orders_by_key_and_leaves_both_operands_alone()
    {
        var one = new ExtractedMap([Node("feature:z", "Z")], []);
        var two = new ExtractedMap([Node("feature:a", "A")], []);

        var both = one.Plus(two);

        Assert.Equal(["feature:a", "feature:z"], both.Nodes.Select(n => n.Key.Value), StringComparer.Ordinal);
        _ = Assert.Single(one.Nodes);
        Assert.Equal(ExtractedMap.Empty, ExtractedMap.Empty.Plus(ExtractedMap.Empty));
    }
}
