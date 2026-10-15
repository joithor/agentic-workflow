using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Proposals;

public sealed class ProposalNodesTests
{
    private static ProposedSurface Surface(string name, string? key = null, NodeKind? kind = null) =>
        new(name, key is null ? null : NodeKey.From(key), kind, Parent: null, Evidence: null, Quote: null);

    [Fact, Trait("Lane", "unit")]
    public void A_surface_that_knows_its_key_keeps_it_in_entry_form()
    {
        Assert.Equal("feature:tab pinning", ProposalNodes.KeyOf(Surface("Tab pinning", "feature:Tab pinning"))?.Value);
        Assert.Equal("surface:command/Pin", ProposalNodes.KeyOf(Surface("Pin", "surface:command/Pin"))?.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_surface_with_a_kind_and_no_key_gets_its_label_key()
    {
        Assert.Equal("feature:tab pinning", ProposalNodes.KeyOf(Surface("  Tab Pinning ", kind: NodeKind.Feature))?.Value);
        Assert.Equal("action:pin tab", ProposalNodes.KeyOf(Surface("Pin tab", kind: NodeKind.Action))?.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, null)]
    [InlineData(null, NodeKind.Surface)]
    [InlineData(null, NodeKind.Record)]
    [InlineData("record:linear:issue/ENG-1", null)]
    [InlineData("decision:abc", null)]
    [InlineData("code:src/a.ts", null)]
    [InlineData("surface:pane-without-a-kind", null)]
    public void A_surface_with_too_little_or_an_unproposable_kind_has_no_key(string? key, NodeKind? kind) =>
        Assert.Null(ProposalNodes.KeyOf(Surface("Thing", key, kind)));

    [Fact, Trait("Lane", "unit")]
    public void The_candidate_is_a_model_node_carrying_the_runs_evidence()
    {
        EvidenceRef[] evidence = [new(RecordKey.From("linear:issue/ENG-1"), "span")];
        var surface = Surface("Tab pinning", kind: NodeKind.Feature) with { Evidence = evidence };

        var node = ProposalNodes.Candidate(NodeKey.From("feature:tab pinning"), surface);

        Assert.Equal(("Tab pinning", Provenance.Model, ProposalRows.Confidence), (node.Label, node.Provenance, node.Confidence));
        Assert.Equal(evidence, node.Evidence);
    }
}
