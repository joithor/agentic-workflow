using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Tests.Review;

namespace Aw.Context.Tests.Overrides;

public sealed class LinkDecisionTests
{
    private static readonly LinkId _groups = SampleDrafts.Link("code:src/tabs/Groups.ts", EdgeType.Implements, "feature:tabs.groups");

    private static MapOverride Decide(OverrideAction action, LinkId link, int minutes = 0) =>
        SampleDrafts.Row(action, "code:src/tabs/Groups.ts", at: SampleDrafts.At.AddMinutes(minutes), link: link);

    [Fact, Trait("Lane", "unit")]
    public void Accepting_a_link_makes_that_edge_human_and_leaves_the_others()
    {
        var applied = OverrideApplier.Apply(SampleDrafts.Draft(), [Decide(OverrideAction.Accept, _groups)]);

        var edges = applied.Graph.Edges.Where(e => e.Type == EdgeType.Implements && e.Provenance != Provenance.Deterministic).ToDictionary(e => e.From.Value, e => e.Provenance, StringComparer.Ordinal);
        Assert.Equal(Provenance.Human, edges["code:src/tabs/Groups.ts"]);
        Assert.Equal(Provenance.Model, edges["code:src/tabs/GroupList.ts"]);
        Assert.Equal(Provenance.Model, applied.Graph.Resolve(SampleDrafts.Key("feature:tabs.groups"))?.Provenance);
    }

    [Fact, Trait("Lane", "unit")]
    public void Rejecting_a_link_removes_the_edge_but_not_its_ends()
    {
        var applied = OverrideApplier.Apply(SampleDrafts.Draft(), [Decide(OverrideAction.Reject, _groups)]);

        Assert.DoesNotContain(applied.Graph.Edges, e => e.From.Value == "code:src/tabs/Groups.ts");
        Assert.NotNull(applied.Graph.Resolve(SampleDrafts.Key("code:src/tabs/Groups.ts")));
        Assert.NotNull(applied.Graph.Resolve(SampleDrafts.Key("feature:tabs.groups")));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_link_decision_holds_over_a_rebuilt_graph_that_has_the_same_edge()
    {
        var reject = Decide(OverrideAction.Reject, _groups);
        var rebuilt = SampleDrafts.Draft();

        Assert.DoesNotContain(OverrideApplier.Apply(rebuilt, [reject]).Graph.Edges, e => e.From.Value == "code:src/tabs/Groups.ts");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_link_decision_whose_edge_is_gone_is_orphaned_not_dropped()
    {
        var gone = LinkIds.For(new GraphEdge(SampleDrafts.Key("code:src/tabs/Old.ts"), SampleDrafts.Key("feature:tabs.groups"), EdgeType.Implements));
        var orphan = SampleDrafts.Row(OverrideAction.Accept, "code:src/tabs/Old.ts", link: gone);

        var applied = OverrideApplier.Apply(SampleDrafts.Draft(), [orphan]);

        Assert.Same(orphan, Assert.Single(applied.Orphaned));
        Assert.Empty(applied.Inapplicable);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_link_decision_has_its_own_id_apart_from_a_node_decision_on_the_same_target()
    {
        var node = SampleDrafts.Row(OverrideAction.Accept, "code:src/tabs/Groups.ts");

        Assert.NotEqual(node.ToId(), Decide(OverrideAction.Accept, _groups).ToId());
    }
}
