using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Tests.Review;

namespace Aw.Context.Tests.Overrides;

public sealed class ProposeOverrideTests
{
    private static readonly EvidenceRef[] _evidence = [new(RecordKey.From("linear:issue/ENG-1"), "span-a")];

    private static MapOverride Propose(string key, string? parent = "feature:tabs", DateTimeOffset? at = null) =>
        new(OverrideAction.Propose, SampleDrafts.Key(key), "service:engine", at ?? SampleDrafts.At, "Tab pinning",
            Proposal: new ProposalPayload("run-1", ContextRepoName.From("aw-split-panes"), NodeKind.Feature, parent is null ? null : SampleDrafts.Key(parent), _evidence, "pin a tab"));

    [Fact, Trait("Lane", "unit")]
    public void A_proposal_adds_an_unconfirmed_model_node_under_its_parent()
    {
        var applied = OverrideApplier.Apply(SampleDrafts.Draft(), [Propose("feature:tabs.pinning")]);

        var node = applied.Graph.Resolve(SampleDrafts.Key("feature:tabs.pinning"));
        Assert.NotNull(node);
        Assert.Equal(("Tab pinning", Provenance.Model, ProposalRows.Confidence), (node.Label, node.Provenance, node.Confidence));
        Assert.Equal(_evidence, node.Evidence);
        var parent = Assert.Single(applied.Graph.Edges, e => e.From.Value == "feature:tabs.pinning");
        Assert.Equal(("feature:tabs", EdgeType.PartOf, Provenance.Model), (parent.To.Value, parent.Type, parent.Provenance));
        Assert.Empty(applied.Orphaned);
        Assert.Empty(applied.Inapplicable);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_proposal_whose_parent_is_not_in_the_graph_adds_the_node_alone()
    {
        var applied = OverrideApplier.Apply(SampleDrafts.Draft(), [Propose("feature:tabs.pinning", parent: "feature:missing")]);

        Assert.NotNull(applied.Graph.Resolve(SampleDrafts.Key("feature:tabs.pinning")));
        Assert.DoesNotContain(applied.Graph.Edges, e => e.From.Value == "feature:tabs.pinning");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_proposal_for_a_node_that_exists_changes_nothing()
    {
        var draft = SampleDrafts.Draft();

        var applied = OverrideApplier.Apply(draft, [Propose("feature:layout")]);

        Assert.Equal(draft.Nodes.Count, applied.Graph.Nodes.Count);
        Assert.Equal(Provenance.Model, applied.Graph.Resolve(SampleDrafts.Key("feature:layout"))?.Provenance);
        Assert.Empty(applied.Orphaned);
    }

    [Fact, Trait("Lane", "unit")]
    public void Accepting_a_proposal_makes_it_human_and_confirms_its_parent_edge()
    {
        var accept = SampleDrafts.Row(OverrideAction.Accept, "feature:tabs.pinning", at: SampleDrafts.At.AddMinutes(1));

        var applied = OverrideApplier.Apply(SampleDrafts.Draft(), [Propose("feature:tabs.pinning"), accept]);

        Assert.Equal(Provenance.Human, applied.Graph.Resolve(SampleDrafts.Key("feature:tabs.pinning"))?.Provenance);
        Assert.Equal(Provenance.Human, Assert.Single(applied.Graph.Edges, e => e.From.Value == "feature:tabs.pinning").Provenance);
    }

    [Fact, Trait("Lane", "unit")]
    public void Rejecting_a_proposal_removes_it()
    {
        var node = ProposalRows.NodeOf(Propose("feature:tabs.pinning"))!;   // why: the row carries a payload
        var reject = SampleDrafts.Row(OverrideAction.Reject, "feature:tabs.pinning", at: SampleDrafts.At.AddMinutes(1), rejection: RejectionIdentity.For(node));

        var applied = OverrideApplier.Apply(SampleDrafts.Draft(), [Propose("feature:tabs.pinning"), reject]);

        Assert.Null(applied.Graph.Resolve(SampleDrafts.Key("feature:tabs.pinning")));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_proposal_without_its_payload_is_inapplicable_and_never_orphaned()
    {
        var bare = new MapOverride(OverrideAction.Propose, SampleDrafts.Key("feature:x"), "service:engine", SampleDrafts.At);

        var applied = OverrideApplier.Apply(SampleDrafts.Draft(), [bare]);

        Assert.Same(bare, Assert.Single(applied.Inapplicable));
        Assert.Empty(applied.Orphaned);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_undo_row_is_never_orphaned_or_inapplicable_even_when_it_names_nothing()
    {
        var stray = SampleDrafts.Undo(SampleDrafts.Row(OverrideAction.Reject, "feature:gone"), SampleDrafts.At.AddMinutes(1));

        var applied = OverrideApplier.Apply(SampleDrafts.Draft(), [stray]);

        Assert.Empty(applied.Orphaned);
        Assert.Empty(applied.Inapplicable);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_proposed_node_is_the_same_for_the_same_row()
    {
        Assert.Equal(ProposalRows.NodeOf(Propose("feature:tabs.pinning")), ProposalRows.NodeOf(Propose("feature:tabs.pinning")));
        Assert.Null(ProposalRows.NodeOf(SampleDrafts.Row(OverrideAction.Accept, "feature:a")));
    }
}
