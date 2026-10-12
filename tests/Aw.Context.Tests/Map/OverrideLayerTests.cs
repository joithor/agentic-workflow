using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Map;

public sealed class OverrideLayerTests
{
    private static readonly DateTimeOffset _at = DateTimeOffset.Parse("2026-08-01T00:00:00Z", CultureInfo.InvariantCulture);

    private static GraphSnapshot Graph() => Maps.Checked(Maps.Fixture()).Graph;

    private static MapOverride Make(OverrideAction action, string target, string? label = null, string? other = null, string? rejection = null) =>
        new(action, Maps.Key(target), "avery", _at, label, other is null ? null : Maps.Key(other), rejection);

    [Fact, Trait("Lane", "unit")]
    public void The_fixture_overrides_reject_the_model_node_and_rename_the_tab_manager()
    {
        var map = Maps.Checked(Maps.Fixture());

        var applied = OverrideApplier.Apply(map.Graph, map.Overrides);

        Assert.Null(applied.Graph.Resolve(Maps.Key("feature:layout.drag-to-split")));
        Assert.Equal("Tabs", applied.Graph.Resolve(Maps.Key("feature:tabs.tab-manager"))?.Label);
        Assert.Empty(applied.Orphaned);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rejection_holds_across_an_unrelated_edit_and_lapses_when_the_quoted_span_changes()
    {
        var node = new GraphNode(Maps.Key("feature:x"), "X", Provenance.Model, 0.5,
            [new EvidenceRef(RecordKey.From("linear:doc/d1"), "span-a"), new EvidenceRef(RecordKey.From("repo:r/a.md"))]);
        var reject = Make(OverrideAction.Reject, "feature:x", rejection: RejectionIdentity.For(node));
        var relabeled = GraphSnapshot.Empty with { Nodes = [node with { Label = "X, reworded", Confidence = 0.7 }] };
        var respanned = GraphSnapshot.Empty with { Nodes = [node with { Evidence = [new EvidenceRef(RecordKey.From("linear:doc/d1"), "span-b")] }] };

        Assert.Empty(OverrideApplier.Apply(relabeled, [reject]).Graph.Nodes);
        _ = Assert.Single(OverrideApplier.Apply(respanned, [reject]).Graph.Nodes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Merge_moves_edges_and_leaves_an_alias()
    {
        var applied = OverrideApplier.Apply(Graph(), [Make(OverrideAction.Merge, "feature:layout.pane-resize", other: "feature:layout.split-pane")]);

        Assert.Equal("feature:layout.split-pane", applied.Graph.Resolve(Maps.Key("feature:layout.pane-resize"))?.Key.Value);
        Assert.Contains(applied.Graph.Edges, e => e.From.Value == "surface:setting/paneResizeStep" && e.To.Value == "feature:layout.split-pane");
        Assert.DoesNotContain(applied.Graph.Edges, e => e.From.Value == e.To.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void Move_replaces_the_part_of_edge_and_only_under_a_feature()
    {
        var moved = OverrideApplier.Apply(Graph(), [Make(OverrideAction.Move, "feature:layout.pane-resize", other: "feature:tabs.tab-manager")]);
        var refused = OverrideApplier.Apply(Graph(), [Make(OverrideAction.Move, "feature:layout.pane-resize", other: "thing:pane-layout")]);

        var parents = moved.Graph.Edges.Where(e => e.Type == EdgeType.PartOf && e.From.Value == "feature:layout.pane-resize").Select(e => e.To.Value);
        Assert.Equal("feature:tabs.tab-manager", Assert.Single(parents));
        _ = Assert.Single(refused.Orphaned);
    }

    [Fact, Trait("Lane", "unit")]
    public void Accept_add_and_delete_change_the_graph_a_reader_sees()
    {
        var applied = OverrideApplier.Apply(Graph(),
        [
            Make(OverrideAction.Accept, "feature:layout.drag-to-split"),
            Make(OverrideAction.Add, "feature:layout.swap-panes", label: "Swap panes", other: "feature:layout.split-pane"),
            Make(OverrideAction.Delete, "surface:keybinding/ctrl+shift+d"),
        ]);

        var accepted = applied.Graph.Resolve(Maps.Key("feature:layout.drag-to-split"));
        Assert.Equal(Trust.Trusted, TrustPolicy.For(accepted!.Provenance, accepted: false));
        Assert.Contains(applied.Graph.Edges, e => e.From.Value == "feature:layout.swap-panes" && e.Type == EdgeType.PartOf);
        Assert.Null(applied.Graph.Resolve(Maps.Key("surface:keybinding/ctrl+shift+d")));
        Assert.DoesNotContain(applied.Graph.Edges, e => e.From.Value == "surface:keybinding/ctrl+shift+d");
    }

    [Fact, Trait("Lane", "unit")]
    public void An_override_whose_target_vanished_is_orphaned_and_the_rest_still_apply()
    {
        var applied = OverrideApplier.Apply(Graph(), [Make(OverrideAction.Rename, "feature:gone", label: "G"), Make(OverrideAction.Rename, "thing:pane-layout", label: "Layout")]);

        Assert.Equal("feature:gone", Assert.Single(applied.Orphaned).Target.Value);
        Assert.Equal("Layout", applied.Graph.Resolve(Maps.Key("thing:pane-layout"))?.Label);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_override_has_the_same_id()
    {
        Assert.Equal(Make(OverrideAction.Delete, "feature:a").Id, Make(OverrideAction.Delete, "feature:a").Id);
        Assert.NotEqual(Make(OverrideAction.Delete, "feature:a").Id, Make(OverrideAction.Delete, "feature:b").Id);
        Assert.Matches("^ovr_[0-9a-f]{16}$", Make(OverrideAction.Delete, "feature:a").Id.Value);
    }
}
