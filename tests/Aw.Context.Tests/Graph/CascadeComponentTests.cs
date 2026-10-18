using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Graph;

/// <summary>The opt-in component hop (SC App. C, C4): node, confirmed implementing code unit, another node, ascended; capped by fan-out.</summary>
public sealed class CascadeComponentTests
{
    private static readonly CascadeOptions _component = new(CascadeHops.Default | CascadeHops.Component);

    [Fact, Trait("Lane", "unit")]
    public void A_confirmed_shared_code_unit_reaches_the_other_feature_at_depth_1_with_the_two_word_path()
    {
        var graph = Graph([("code:src/shared.ts", "feature:a", Provenance.Human), ("code:src/shared.ts", "feature:b", Provenance.Deterministic)]);

        var result = Cascade.Affects(graph, NodeKey.From("feature:a"), depth: 2, _component);

        var affected = Assert.Single(result.Affected);
        Assert.Equal(("feature:b", 1), (affected.Node.Value, affected.Depth));
        Assert.Equal(["feature:a", "component code:src/shared.ts", "implements feature:b"], affected.Path);
        Assert.Equal([EdgeType.Implements, EdgeType.Implements], affected.Edges.Select(e => e.Type));
        Assert.Empty(result.SkippedComponents);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_the_component_hop_the_default_walk_does_not_reach_it()
    {
        var graph = Graph([("code:src/shared.ts", "feature:a", Provenance.Human), ("code:src/shared.ts", "feature:b", Provenance.Human)]);

        Assert.Empty(Cascade.Affects(graph, NodeKey.From("feature:a"), depth: 2).Affected);
        Assert.Empty(Cascade.Affects(graph, NodeKey.From("feature:a"), depth: 2, new CascadeOptions()).Affected);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_suggested_implements_edge_never_makes_a_component_hop_in_either_direction()
    {
        var onTheOtherSide = Graph([("code:src/shared.ts", "feature:a", Provenance.Human), ("code:src/shared.ts", "feature:b", Provenance.Model)]);
        var onTheNearSide = Graph([("code:src/shared.ts", "feature:a", Provenance.Model), ("code:src/shared.ts", "feature:b", Provenance.Human)]);

        Assert.Empty(Cascade.Affects(onTheOtherSide, NodeKey.From("feature:a"), depth: 2, _component).Affected);
        Assert.Empty(Cascade.Affects(onTheNearSide, NodeKey.From("feature:a"), depth: 2, _component).Affected);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_unit_over_the_cap_is_skipped_and_reported_with_its_feature_count_and_a_unit_at_the_cap_is_walked()
    {
        var edges = Enumerable.Range(0, 6).Select(i => ("code:src/util.ts", string.Create(System.Globalization.CultureInfo.InvariantCulture, $"feature:f{i}"), Provenance.Human)).ToList();
        var graph = Graph(edges);

        var capped = Cascade.Affects(graph, NodeKey.From("feature:f0"), depth: 2, new CascadeOptions(CascadeHops.Component, ComponentFanOut: 5));
        var atCap = Cascade.Affects(graph, NodeKey.From("feature:f0"), depth: 2, new CascadeOptions(CascadeHops.Component, ComponentFanOut: 6));

        Assert.Empty(capped.Affected);
        Assert.Equal([new SkippedComponent("code:src/util.ts", 6)], capped.SkippedComponents);
        Assert.Equal(5, atCap.Affected.Count);
        Assert.Empty(atCap.SkippedComponents);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_skipped_unit_whose_features_are_all_already_reached_is_not_reported()
    {
        var edges = Enumerable.Range(0, 3).Select(i => ("code:src/util.ts", string.Create(System.Globalization.CultureInfo.InvariantCulture, $"feature:f{i}"), Provenance.Human)).ToList();
        edges.Add(("code:src/util.ts", "feature:f3", Provenance.Human));
        var graph = Graph(edges, parts: [("feature:f1", "feature:f0"), ("feature:f2", "feature:f0"), ("feature:f3", "feature:f0")]);

        var result = Cascade.Affects(graph, NodeKey.From("feature:f0"), depth: 2, new CascadeOptions(CascadeHops.Component, ComponentFanOut: 2));

        Assert.Empty(result.SkippedComponents);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_hop_chains_at_cost_1_per_hop_up_to_the_depth()
    {
        var graph = Graph([("code:src/ab.ts", "feature:a", Provenance.Human), ("code:src/ab.ts", "feature:b", Provenance.Human), ("code:src/bc.ts", "feature:b", Provenance.Human), ("code:src/bc.ts", "feature:c", Provenance.Human)]);

        var depth1 = Cascade.Affects(graph, NodeKey.From("feature:a"), depth: 1, _component);
        var depth2 = Cascade.Affects(graph, NodeKey.From("feature:a"), depth: 2, _component);

        Assert.Equal(["feature:b"], depth1.Affected.Select(a => a.Node.Value), StringComparer.Ordinal);
        Assert.Equal([("feature:b", 1), ("feature:c", 2)], depth2.Affected.Select(a => (a.Node.Value, a.Depth)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Leaving_dependency_out_of_the_hops_stops_the_walk_from_taking_that_hop()
    {
        var graph = new GraphSnapshot(
            [new GraphNode(NodeKey.From("feature:a"), "a"), new GraphNode(NodeKey.From("feature:b"), "b")],
            [new GraphEdge(NodeKey.From("feature:b"), NodeKey.From("feature:a"), EdgeType.DependsOn)], [], []);

        var with = Cascade.Affects(graph, NodeKey.From("feature:a"), depth: 2, new CascadeOptions(CascadeHops.Dependency));
        var without = Cascade.Affects(graph, NodeKey.From("feature:a"), depth: 2, new CascadeOptions(CascadeHops.State | CascadeHops.Flow | CascadeHops.Component));

        Assert.Equal(["feature:b"], with.Affected.Select(a => a.Node.Value), StringComparer.Ordinal);
        Assert.Empty(without.Affected);
    }

    [Fact, Trait("Lane", "unit")]
    public void Leaving_flow_out_of_the_hops_stops_the_walk_from_taking_that_hop()
    {
        var graph = new GraphSnapshot(
            [new GraphNode(NodeKey.From("surface:s1"), "s1"), new GraphNode(NodeKey.From("surface:s2"), "s2"), new GraphNode(NodeKey.From("flow:f"), "f")],
            [new GraphEdge(NodeKey.From("surface:s1"), NodeKey.From("flow:f"), EdgeType.StepOf, Order: 1), new GraphEdge(NodeKey.From("surface:s2"), NodeKey.From("flow:f"), EdgeType.StepOf, Order: 2)], [], []);

        var with = Cascade.Affects(graph, NodeKey.From("surface:s1"), depth: 2, new CascadeOptions(CascadeHops.Flow));
        var without = Cascade.Affects(graph, NodeKey.From("surface:s1"), depth: 2, new CascadeOptions(CascadeHops.State | CascadeHops.Dependency | CascadeHops.Component));

        Assert.Equal(["surface:s2"], with.Affected.Select(a => a.Node.Value), StringComparer.Ordinal);
        Assert.Empty(without.Affected);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_fan_out_of_1_walks_no_unit_because_the_anchors_own_feature_counts()
    {
        var graph = Graph([("code:src/shared.ts", "feature:a", Provenance.Human), ("code:src/shared.ts", "feature:b", Provenance.Human)]);

        var result = Cascade.Affects(graph, NodeKey.From("feature:a"), depth: 2, new CascadeOptions(CascadeHops.Component, ComponentFanOut: 1));

        Assert.Empty(result.Affected);
        Assert.Equal([new SkippedComponent("code:src/shared.ts", 2)], result.SkippedComponents);
    }

    private static GraphSnapshot Graph(IReadOnlyList<(string Code, string Feature, Provenance Provenance)> implements, IReadOnlyList<(string Child, string Parent)>? parts = null)
    {
        var keys = implements.SelectMany(e => new[] { e.Code, e.Feature }).Distinct(StringComparer.Ordinal).ToList();
        var nodes = keys.Select(k => new GraphNode(NodeKey.From(k), k)).ToList();
        var edges = implements.Select(e => new GraphEdge(NodeKey.From(e.Code), NodeKey.From(e.Feature), EdgeType.Implements, e.Provenance)).ToList();
        edges.AddRange((parts ?? []).Select(p => new GraphEdge(NodeKey.From(p.Child), NodeKey.From(p.Parent), EdgeType.PartOf)));
        return new GraphSnapshot(nodes, edges, [], []);
    }
}
