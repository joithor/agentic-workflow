using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Lenses;

public sealed class LensesBuilderTests
{
    private const string Anchor = "feature:tabs.tab-manager";

    private static GraphSnapshot Tabs()
    {
        var map = Maps.Checked(Maps.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "map-tabs.json"))));
        return OverrideApplier.Apply(map.Graph, map.Overrides).Graph;
    }

    private static LensesView Lenses(string anchor = Anchor, int depth = 2) =>
        LensesBuilder.Build("v13@7c3e1a2", draft: false, Tabs(), NodeKey.From(anchor), depth).Value;

    private static string[] Keys(LensBand band) => [.. band.Items.Select(i => i.Key.Value)];

    [Fact, Trait("Lane", "unit")]
    public void The_view_names_the_map_the_anchor_and_that_the_symbols_are_unavailable()
    {
        var view = Lenses();

        Assert.Equal(("v13@7c3e1a2", false, Anchor), (view.Map, view.Draft, view.Anchor.Value));
        Assert.Equal(("unavailable", "index not built"), (view.Symbols.Status, view.Symbols.Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_product_band_holds_the_own_scope_then_neighbors_without_repeating_a_node()
    {
        var view = Lenses(depth: 3);

        Assert.Equal(
            [Anchor, "action:tab-groups.group-selected", "action:tab-manager.restore-layout", "feature:tabs.tab-groups", "surface:command/group-tabs", "surface:command/switch-tab", "thing:pane-layout"],
            Keys(view.Product));
        Assert.Equal(6, view.Product.Items.Count(i => i.Relation == LensRelation.Own));
        Assert.Equal(LensRelation.Neighbor, view.Product.Items.Single(i => i.Key.Value == "thing:pane-layout").Relation);
        Assert.False(view.Product.Truncated);
    }

    [Fact, Trait("Lane", "unit")]
    public void At_depth_0_there_are_no_neighbors() =>
        Assert.DoesNotContain(Lenses(depth: 0).Product.Items, i => i.Relation != LensRelation.Own);

    [Fact, Trait("Lane", "unit")]
    public void The_intent_band_holds_the_records_and_decisions_that_hang_on_the_scope()
    {
        var view = Lenses();

        Assert.Equal(3, view.Intent.Items.Count);
        Assert.Contains(view.Intent.Items, i => i.Key.Value.StartsWith("record:linear:issue/0e5a1b00-0000-4000-8000-000000000203", StringComparison.Ordinal));
        Assert.Equal(2, view.Intent.Items.Count(i => i.Key.Kind == NodeKind.Decision));
        Assert.All(view.Intent.Items, i => Assert.Equal(LensRelation.Own, i.Relation));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_code_band_lists_files_that_implement_scope_nodes_and_marks_the_suggested_one_unconfirmed()
    {
        var view = Lenses();

        Assert.Equal(["code:src/tabs/TabGroups.ts", "code:src/tabs/TabManager.ts"], Keys(view.Code));
        var suggested = view.Code.Items.Single(i => i.Key.Value == "code:src/tabs/TabGroups.ts");
        Assert.Equal((Provenance.Model, 0.74, false), (suggested.Provenance, suggested.Confidence, suggested.Confirmed));
        Assert.Equal(SampleLink("code:src/tabs/TabGroups.ts", "feature:tabs.tab-groups"), suggested.Link);
        var confirmed = view.Code.Items.Single(i => i.Key.Value == "code:src/tabs/TabManager.ts");
        Assert.True(confirmed.Confirmed);
        Assert.True(confirmed.Link.HasValue);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_cross_band_edges_carry_ids_and_confirmation()
    {
        var view = Lenses();

        Assert.Equal(new Dictionary<EdgeType, int> { [EdgeType.Mentions] = 1, [EdgeType.Decides] = 2, [EdgeType.Supersedes] = 1, [EdgeType.Implements] = 3 },
            view.Edges.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count()));
        var model = Assert.Single(view.Edges, e => !e.Confirmed);
        Assert.Equal((EdgeType.Implements, "code:src/tabs/TabGroups.ts", 0.74), (model.Type, model.From.Value, model.Confidence));
        Assert.Equal(SampleLink("code:src/tabs/TabGroups.ts", "feature:tabs.tab-groups"), model.Link);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_decision_that_supersedes_another_on_a_scope_node_is_a_conflict()
    {
        var conflict = Assert.Single(Lenses().Conflicts);

        Assert.Equal("supersedes", conflict.Kind);
        Assert.StartsWith("decision:ef895edc", conflict.Newer.Value, StringComparison.Ordinal);
        Assert.StartsWith("decision:d0363522", conflict.Older.Value, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_anchor_with_no_decisions_has_no_conflicts()
    {
        var graph = new GraphBuilder().Edge("surface:screen/s", EdgeType.PartOf, "feature:a").Build();

        Assert.Empty(LensesBuilder.Build("v1@a", draft: false, graph, NodeKey.From("feature:a"), 2).Value.Conflicts);
    }

    [Fact, Trait("Lane", "unit")]
    public void Coverage_counts_the_own_scope_and_gaps_name_what_each_node_lacks()
    {
        var view = Lenses();

        Assert.Equal(new LensCoverage(Scope: 6, Covered: 2, Confirmed: 6, Implemented: 3), view.Coverage);
        Assert.Equal(
        [
            "action:tab-groups.group-selected: no intent, no code",
            "action:tab-manager.restore-layout: no intent",
            "surface:command/group-tabs: no intent, no code",
            "surface:command/switch-tab: no intent, no code",
        ], view.Gaps);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_model_only_node_is_not_confirmed_in_the_coverage()
    {
        var graph = new GraphBuilder().Node("feature:a").Node("feature:b", provenance: Provenance.Model).Edge("feature:b", EdgeType.PartOf, "feature:a").Build();

        var view = LensesBuilder.Build("v1@a", draft: false, graph, NodeKey.From("feature:a"), 1).Value;

        Assert.Equal((2, 1), (view.Coverage.Scope, view.Coverage.Confirmed));
        Assert.False(view.Product.Items.Single(i => i.Key.Value == "feature:b").Confirmed);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_node_the_cascade_reaches_is_an_affects_item_and_a_neighbor_is_listed_once_as_a_neighbor()
    {
        var graph = new GraphBuilder()
            .Edge("surface:screen/s", EdgeType.PartOf, "feature:a").Edge("surface:screen/s", EdgeType.Writes, "thing:t")
            .Edge("action:r1", EdgeType.Reads, "thing:t").Edge("action:r1", EdgeType.On, "surface:screen/r").Edge("surface:screen/r", EdgeType.PartOf, "feature:r")
            .Edge("feature:d", EdgeType.DependsOn, "feature:a")
            .Build();

        var view = LensesBuilder.Build("v1@a", draft: false, graph, NodeKey.From("feature:a"), 2).Value;

        Assert.Equal(LensRelation.Affects, view.Product.Items.Single(i => i.Key.Value == "feature:r").Relation);
        Assert.Equal(1, view.Product.Items.Count(i => i.Key.Value == "feature:d"));
        Assert.Equal(LensRelation.Neighbor, view.Product.Items.Single(i => i.Key.Value == "feature:d").Relation);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_band_is_cut_at_100_items_and_says_so()
    {
        var builder = new GraphBuilder().Node("feature:root");
        for (var i = 0; i < 120; i++)
        {
            _ = builder.Edge("feature:child-" + i.ToString("D3", CultureInfo.InvariantCulture), EdgeType.PartOf, "feature:root");
        }

        var view = LensesBuilder.Build("v1@a", draft: false, builder.Build(), NodeKey.From("feature:root"), 1).Value;

        Assert.Equal(LensesBuilder.BandCap, view.Product.Items.Count);
        Assert.True(view.Product.Truncated);
        Assert.Equal(121, view.Coverage.Scope);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_alias_anchors_the_node_it_names_and_an_unknown_anchor_is_003()
    {
        var graph = new GraphBuilder().Node("feature:a").Alias("feature:old-a", "feature:a").Build();

        Assert.Equal("feature:a", LensesBuilder.Build("v1@a", draft: false, graph, NodeKey.From("feature:old-a"), 1).Value.Anchor.Value);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, LensesBuilder.Build("v1@a", draft: false, graph, NodeKey.From("feature:nope"), 1).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_draft_flag_and_map_label_pass_through()
    {
        var view = LensesBuilder.Build("v14@abc", draft: true, Tabs(), NodeKey.From(Anchor), 1).Value;

        Assert.Equal(("v14@abc", true), (view.Map, view.Draft));
    }

    private static LinkId SampleLink(string from, string to) => LinkIds.For(new GraphEdge(NodeKey.From(from), NodeKey.From(to), EdgeType.Implements));
}
