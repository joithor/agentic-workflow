using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Subgraph;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Subgraph;

public sealed class SubgraphBuilderTests
{
    private const string A = "feature:a";
    private const string B = "feature:b";
    private const string Surface = "surface:command/x";
    private const string Act = "action:do";
    private const string Rec = "record:linear:AB-1";
    private const string D1 = "decision:d1";
    private const string D0 = "decision:d0";
    private const string FileA = "code:src/a.ts";
    private const string FileB = "code:src/b.ts";
    private static readonly DateTimeOffset _asOf = DateTimeOffset.Parse("2026-10-14T09:12:40Z", CultureInfo.InvariantCulture);

    private static readonly LensBasis _basis = new("v1", Draft: false, _asOf);

    // A is part of nothing; B and the surface are part of A; the action is on the surface; a record mentions A; D1 decides A and supersedes D0 (which decides A too);
    // two files implement A and B. C is a lone feature decided by E1, which supersedes E0.
    private static GraphSnapshot Graph() => new GraphBuilder()
        .Edge(B, EdgeType.PartOf, A).Edge(Surface, EdgeType.PartOf, A).Edge(Act, EdgeType.On, Surface)
        .Edge(Rec, EdgeType.Mentions, A).Edge(D1, EdgeType.Decides, A).Edge(D0, EdgeType.Decides, A).Edge(D1, EdgeType.Supersedes, D0)
        .Edge(FileA, EdgeType.Implements, A).Edge(FileB, EdgeType.Implements, B)
        .Edge("decision:e1", EdgeType.Decides, "feature:c").Edge("decision:e0", EdgeType.Decides, "feature:c").Edge("decision:e1", EdgeType.Supersedes, "decision:e0")
        .Build();

    private static SubgraphView Of(string[] keys, int depth = 0, GraphSnapshot? graph = null) =>
        SubgraphBuilder.Build(_basis, graph ?? Graph(), keys, depth, []).Value;

    [Fact, Trait("Lane", "unit")]
    public void Given_keys_are_placed_by_kind_in_the_three_bands()
    {
        var view = Of([A, Rec, D1, FileA]);

        Assert.Equal([A, Rec, D1, FileA], view.Nodes.Select(n => n.Value), StringComparer.Ordinal);
        Assert.Equal([A], view.Bands.Product.Own.Select(i => i.Node.Value), StringComparer.Ordinal);
        Assert.Equal([Rec], view.Bands.Intent.Records.Select(r => r.Key.Value), StringComparer.Ordinal);
        Assert.Equal([D1], view.Bands.Intent.Decisions.Select(d => d.Node.Value), StringComparer.Ordinal);
        Assert.Equal(["src/a.ts"], view.Bands.Code.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal(1, view.SchemaVersion);
        Assert.Equal([0, 0, 0, 0], new[] { view.Bands.Product.Own[0].Depth, view.Bands.Intent.Records[0].Depth, view.Bands.Intent.Decisions[0].Depth, view.Bands.Code.Files[0].Depth }.Select(d => d ?? -1));
    }

    [Fact, Trait("Lane", "unit")]
    public void Edges_are_only_among_returned_nodes()
    {
        var view = Of([A, FileA]);

        var returned = view.Nodes.Select(n => n.Value).ToHashSet(StringComparer.Ordinal);
        Assert.All(view.Edges, e => Assert.True(returned.Contains(e.From.Value) && returned.Contains(e.To.Value)));
        var edge = Assert.Single(view.Edges);
        Assert.Equal((FileA, A, EdgeType.Implements), (edge.From.Value, edge.To.Value, edge.Type));
    }

    [Fact, Trait("Lane", "unit")]
    public void Depth_0_returns_exactly_the_given_keys()
    {
        var view = Of([A, B]);

        Assert.Empty(view.Bands.Product.Neighbors);
        Assert.Empty(view.Bands.Product.Affects);
        Assert.Empty(view.Bands.Intent.Records);
        Assert.Empty(view.Bands.Code.Files);
        Assert.Equal([A, B], view.Bands.Product.Own.Select(i => i.Node.Value), StringComparer.Ordinal);
        Assert.All(view.Bands.Product.Own, i => Assert.Equal(0, i.Depth));
    }

    [Fact, Trait("Lane", "unit")]
    public void Depth_2_widens_through_neighbors_and_marks_depth()
    {
        var view = Of([A], depth: 2);

        var near = view.Bands.Product.Neighbors.ToDictionary(n => n.Node.Value, n => n.Depth, StringComparer.Ordinal);
        Assert.Equal((1, 1, 2), (near[B], near[Surface], near[Act]));
        Assert.Equal([1], view.Bands.Intent.Records.Select(r => r.Depth ?? -1));
        Assert.Equal([1, 1], view.Bands.Intent.Decisions.Select(d => d.Depth ?? -1));
        Assert.Equal([1], view.Bands.Code.Files.Select(f => f.Depth ?? -1).Distinct());
        Assert.Equal([A], view.Nodes.Select(n => n.Value), StringComparer.Ordinal);
        Assert.Contains(view.Bands.Product.Neighbors, n => n.Node.Value == Act && n.Path.Count == 2);
    }

    [Fact, Trait("Lane", "unit")]
    public void Absent_key_is_in_missing_in_request_order()
    {
        var view = Of(["feature:zzz", A, "feature:yyy", "feature:zzz"]);

        Assert.Equal(["feature:zzz", "feature:yyy"], view.Missing, StringComparer.Ordinal);
        Assert.Equal([A], view.Nodes.Select(n => n.Value), StringComparer.Ordinal);
        Assert.Equal([A], view.Searched.Anchors, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Malformed_key_is_006_naming_it()
    {
        var result = SubgraphBuilder.Build(_basis, Graph(), [A, "not a key"], 0, []);

        Assert.True(result.IsError);
        Assert.Equal(ContextErrors.QueryInvalid.Code, result.Error.Code);
        Assert.Contains("not a key", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Band_over_100_truncated_with_full_counts()
    {
        var builder = new GraphBuilder();
        var keys = Enumerable.Range(0, 150).Select(i => $"feature:f{i:D3}").ToArray();
        foreach (var key in keys)
        {
            _ = builder.Node(key);
        }

        var view = Of(keys, graph: builder.Build());

        Assert.True(view.Bands.Product.Truncated);
        Assert.Equal(150, view.Bands.Product.Counts.Own);
        Assert.Equal(keys.Take(100), view.Bands.Product.Own.Select(i => i.Node.Value), StringComparer.Ordinal);
        Assert.Equal(150, view.Nodes.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void Coverage_is_over_the_given_product_nodes()
    {
        var view = Of([A, B, Rec, FileA], depth: 1);

        Assert.Equal(2, view.Coverage.Scope);
        Assert.Equal((1, 2), (view.Coverage.Intent.Covered, view.Coverage.Intent.Of));
        Assert.Equal(2, view.Coverage.Code.Of);
        Assert.NotNull(view.Coverage.Map);
        Assert.Null(view.Coverage.ByAnchor);
    }

    [Fact, Trait("Lane", "unit")]
    public void Conflicts_on_returned_nodes_only()
    {
        var withA = Of([A]);
        var withC = Of(["feature:c"]);

        Assert.All(withA.Conflicts, c => Assert.Equal(A, c.Node.Value));
        _ = Assert.Single(withA.Conflicts);
        Assert.All(withC.Conflicts, c => Assert.Equal("feature:c", c.Node.Value));
        Assert.Empty(Of([B]).Conflicts);
    }

    [Fact, Trait("Lane", "unit")]
    public void More_than_1000_keys_is_006()
    {
        var keys = Enumerable.Range(0, QueryRules.MaxSubgraphPost + 1).Select(i => $"feature:f{i}").ToArray();

        var over = SubgraphBuilder.Build(_basis, Graph(), keys, 0, []);
        var none = SubgraphBuilder.Build(_basis, Graph(), [], 0, []);
        var edge = SubgraphBuilder.Build(_basis, Graph(), [.. keys.Take(QueryRules.MaxSubgraphPost)], 0, []);

        Assert.Equal(ContextErrors.QueryInvalid.Code, over.Error.Code);
        Assert.Equal(ContextErrors.QueryInvalid.Code, none.Error.Code);
        Assert.True(edge.IsSuccess);
        Assert.Equal(QueryRules.MaxSubgraphPost, edge.Value.Missing.Count);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(-1)]
    [InlineData(5)]
    public void Depth_outside_0_to_4_is_006(int depth)
    {
        var result = SubgraphBuilder.Build(_basis, Graph(), [A], depth, []);

        Assert.True(result.IsError);
        Assert.Equal(ContextErrors.QueryInvalid.Code, result.Error.Code);
    }
}
