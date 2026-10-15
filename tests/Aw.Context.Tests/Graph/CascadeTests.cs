using Aw.Context.Core.Graph;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Graph;

/// <summary>The cascade (spec §6.1): own scope free, typed steps cost 1, ascend free and never re-entering own scope, a depth cap and shortest paths.</summary>
public sealed class CascadeTests
{
    private const string Orientation = "feature:layout.split-pane.orientation";
    private const string TabManager = "feature:tabs.tab-manager";

    [Fact, Trait("Lane", "unit")]
    public void Orientation_affects_the_tab_manager_at_depth_1_through_pane_layout()
    {
        var result = Cascade.Affects(FixtureGraph.Applied(), FixtureGraph.Key(Orientation), depth: 2);

        var affected = Assert.Single(result.Affected);
        Assert.Equal((TabManager, 1), (affected.Node.Value, affected.Depth));
        Assert.Equal([Orientation, "owns surface:command/toggle-orientation", "owns action:split-pane.toggle-orientation", "writes thing:pane-layout", "reads action:tab-manager.restore-layout"], affected.Path);
        Assert.Equal([EdgeType.PartOf, EdgeType.On, EdgeType.Writes, EdgeType.Reads], affected.Edges.Select(e => e.Type));
    }

    [Fact, Trait("Lane", "unit")]
    public void Own_scope_is_the_anchor_its_surfaces_and_their_actions_and_never_its_sibling()
    {
        var result = Cascade.Affects(FixtureGraph.Applied(), FixtureGraph.Key(Orientation), depth: 2);

        Assert.Equal([Orientation, "action:split-pane.toggle-orientation", "surface:command/toggle-orientation"], result.OwnScope.Select(k => k.Value), StringComparer.Ordinal);
        Assert.DoesNotContain(result.Affected, a => a.Node.Value == "feature:layout.pane-resize");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_parent_feature_takes_its_descendant_features_into_own_scope()
    {
        var result = Cascade.Affects(FixtureGraph.Applied(), FixtureGraph.Key("feature:layout.split-pane"), depth: 2);

        Assert.Contains(result.OwnScope, k => k.Value == "feature:layout.pane-resize");
        Assert.Contains(result.OwnScope, k => k.Value == "action:pane-resize.resize-focused");
        Assert.Equal([TabManager], result.Affected.Select(a => a.Node.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_sibling_is_not_affected_unless_a_step_reaches_it()
    {
        var graph = Features("a", "b").Edge("feature:a", EdgeType.PartOf, "feature:p").Edge("feature:b", EdgeType.PartOf, "feature:p")
            .Edge("action:a1", EdgeType.Writes, "thing:t").Build();

        var result = Cascade.Affects(graph, NodeKey.From("feature:a"), depth: 4);

        Assert.Empty(result.Affected);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_ascended_feature_does_not_re_enter_own_scope_but_its_reached_action_steps_on()
    {
        var graph = Features("a", "b", "c", "d")
            .Edge("action:a1", EdgeType.Writes, "thing:t")
            .Edge("action:b1", EdgeType.Reads, "thing:t")
            .Edge("action:b1", EdgeType.Writes, "thing:v")
            .Edge("action:d1", EdgeType.Reads, "thing:v")
            .Edge("action:b2", EdgeType.On, "surface:screen/b2").Edge("surface:screen/b2", EdgeType.PartOf, "feature:b")
            .Edge("action:b2", EdgeType.Writes, "thing:u")
            .Edge("action:c1", EdgeType.Reads, "thing:u")
            .Build();

        var result = Cascade.Affects(graph, NodeKey.From("feature:a"), depth: 3);

        Assert.Equal([("feature:b", 1), ("feature:d", 2)], result.Affected.Select(a => (a.Node.Value, a.Depth)));
        Assert.Equal(["feature:a", "writes thing:t", "reads action:b1", "writes thing:v", "reads action:d1"], result.Affected[1].Path);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_dependency_hop_costs_one_and_chains()
    {
        var graph = new GraphBuilder().Edge("feature:b", EdgeType.DependsOn, "feature:a").Edge("feature:c", EdgeType.DependsOn, "feature:b").Build();

        var result = Cascade.Affects(graph, NodeKey.From("feature:a"), depth: 2);

        Assert.Equal([("feature:b", 1), ("feature:c", 2)], result.Affected.Select(a => (a.Node.Value, a.Depth)));
        Assert.Equal(["feature:a", "depended-on-by feature:b", "depended-on-by feature:c"], result.Affected[1].Path);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_flow_hop_reaches_the_next_step_of_a_shared_flow()
    {
        var graph = Features("a", "g")
            .Edge("action:a1", EdgeType.StepOf, "flow:f", order: 1)
            .Edge("action:g1", EdgeType.StepOf, "flow:f", order: 2)
            .Build();

        var affected = Assert.Single(Cascade.Affects(graph, NodeKey.From("feature:a"), depth: 1).Affected);

        Assert.Equal(("feature:g", 1), (affected.Node.Value, affected.Depth));
        Assert.Equal(["feature:a", "step-of flow:f", "next action:g1"], affected.Path);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void The_depth_cap_stops_the_walk(int depth, int expected)
    {
        var graph = new GraphBuilder().Edge("feature:b", EdgeType.DependsOn, "feature:a").Edge("feature:c", EdgeType.DependsOn, "feature:b").Build();

        Assert.Equal(expected, Cascade.Affects(graph, NodeKey.From("feature:a"), depth).Affected.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cycle_ends_and_each_node_is_reported_once_at_its_shortest_depth()
    {
        var graph = Features("a", "b")
            .Edge("action:a1", EdgeType.Writes, "thing:t").Edge("action:b1", EdgeType.Reads, "thing:t")
            .Edge("action:b1", EdgeType.Writes, "thing:u").Edge("action:a1", EdgeType.Reads, "thing:u")
            .Build();

        var result = Cascade.Affects(graph, NodeKey.From("feature:a"), depth: 4);

        Assert.Equal([("feature:b", 1)], result.Affected.Select(a => (a.Node.Value, a.Depth)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reached_action_with_no_surface_is_reported_as_itself()
    {
        var graph = Features("a").Edge("action:a1", EdgeType.Writes, "thing:t").Edge("action:loose", EdgeType.Reads, "thing:t").Build();

        var affected = Assert.Single(Cascade.Affects(graph, NodeKey.From("feature:a"), depth: 1).Affected);

        Assert.Equal("action:loose", affected.Node.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_thing_anchor_affects_what_reads_it()
    {
        var affected = Assert.Single(Cascade.Affects(FixtureGraph.Applied(), FixtureGraph.Key("thing:pane-layout"), depth: 1).Affected);

        Assert.Equal(TabManager, affected.Node.Value);
        Assert.Equal(["thing:pane-layout", "reads action:tab-manager.restore-layout"], affected.Path);
    }

    /// <summary>Each feature 'feature:x' with a surface 'surface:screen/x' and an action 'action:x1' on it.</summary>
    private static GraphBuilder Features(params string[] names)
    {
        var builder = new GraphBuilder();
        foreach (var name in names)
        {
            _ = builder.Edge($"surface:screen/{name}", EdgeType.PartOf, $"feature:{name}").Edge($"action:{name}1", EdgeType.On, $"surface:screen/{name}");
        }

        return builder;
    }
}
