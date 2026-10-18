using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Lenses;

/// <summary>The opt-in component hop through lenses (C4): the related feature, the skipped list and the hops echo; a call without the hop is unchanged.</summary>
public sealed class LensesComponentHopTests
{
    private static readonly DateTimeOffset _asOf = DateTimeOffset.Parse("2026-10-14T09:12:40Z", CultureInfo.InvariantCulture);
    private static readonly CascadeOptions _on = new(CascadeHops.Default | CascadeHops.Component);

    [Fact, Trait("Lane", "unit")]
    public void With_the_hop_a_feature_sharing_a_confirmed_code_unit_is_an_affects_item_with_the_two_word_path_and_the_hops_echo_names_it()
    {
        var view = LensesBuilder.Build(Basis(_on), Shared(2), NodeKey.From("feature:f0"), depth: 1).Value;

        var reach = Assert.Single(view.Bands.Product.Affects);
        Assert.Equal(("feature:f1", 1), (reach.Node.Value, reach.Depth));
        Assert.Equal(["feature:f0", "component code:src/shared.ts", "implements feature:f1"], reach.Path);
        Assert.Equal(["state", "dependency", "flow", "component"], view.Searched.Hops);
        Assert.Null(view.SkippedComponents);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_the_hop_the_answer_has_no_such_item_no_skipped_list_and_the_three_default_hops()
    {
        var view = LensesBuilder.Build(Basis(CascadeOptions.Default), Shared(2), NodeKey.From("feature:f0"), depth: 1).Value;

        Assert.Empty(view.Bands.Product.Affects);
        Assert.Equal(["state", "dependency", "flow"], view.Searched.Hops);
        Assert.Null(view.SkippedComponents);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_unit_over_the_cap_is_listed_top_level_with_its_feature_count()
    {
        var view = LensesBuilder.Build(Basis(_on with { ComponentFanOut = 5 }), Shared(6), NodeKey.From("feature:f0"), depth: 1).Value;

        Assert.Empty(view.Bands.Product.Affects);
        Assert.Equal([new SkippedComponent("code:src/shared.ts", 6)], view.SkippedComponents);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_set_takes_the_hop_too_and_lists_each_skipped_unit_once()
    {
        var view = LensesBuilder.BuildSet(Basis(_on with { ComponentFanOut = 5 }), Shared(6), ["feature:f0", "feature:f1"], depth: 1).Value;

        Assert.Equal([new SkippedComponent("code:src/shared.ts", 6)], view.SkippedComponents);
        Assert.Equal(["state", "dependency", "flow", "component"], view.Searched.Hops);
    }

    private static LensBasis Basis(CascadeOptions options) => new("v1", Draft: false, _asOf) { Cascade = options };

    private static GraphSnapshot Shared(int features)
    {
        var keys = Enumerable.Range(0, features).Select(i => string.Create(CultureInfo.InvariantCulture, $"feature:f{i}")).ToList();
        var nodes = keys.Select(k => new GraphNode(NodeKey.From(k), k)).Append(new GraphNode(NodeKey.From("code:src/shared.ts"), "shared.ts")).ToList();
        var edges = keys.Select(k => new GraphEdge(NodeKey.From("code:src/shared.ts"), NodeKey.From(k), EdgeType.Implements)).ToList();
        return new GraphSnapshot(nodes, edges, Aliases: [], Decisions: []);
    }
}
