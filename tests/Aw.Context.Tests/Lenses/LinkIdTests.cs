using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Lenses;

public sealed class LinkIdTests
{
    private static readonly GraphEdge _edge = new(NodeKey.From("code:src/a.ts"), NodeKey.From("feature:a"), EdgeType.Implements);

    [Fact, Trait("Lane", "unit")]
    public void The_same_fact_gets_the_same_id_whatever_its_provenance()
    {
        var first = LinkIds.For(_edge);
        var again = LinkIds.For(_edge with { Provenance = Provenance.Model, Confidence = 0.5 });

        Assert.Equal(first, again);
        Assert.Matches("^edge:[0-9a-f]{16}$", first.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void Another_type_or_end_gets_another_id() =>
        Assert.NotEqual(LinkIds.For(_edge), LinkIds.For(_edge with { Type = EdgeType.Tests }));
}
