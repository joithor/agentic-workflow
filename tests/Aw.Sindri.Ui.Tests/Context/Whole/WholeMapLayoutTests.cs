using System.Globalization;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>
/// Where the whole map sits (whole-map spec §3.2). Built on the DRAFT goldens under Golden/SpecShaped/context-whole (copied from the design spec's
/// draft-goldens; swapped for context's real goldens when W1 lands): the same answer gives the same numbers, columns follow the barycenter of the
/// edges between areas with the unassigned area last, and the three bands line up across every column.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapLayoutTests
{
    internal static WholeMapView Draft(string name) =>
        AwJson.Deserialize<WholeMapView>(System.Text.Encoding.UTF8.GetBytes(GoldenFiles.SpecShaped("context-whole", name)), SindriApiClient.Json).Value;

    [Fact]
    public void The_same_answer_places_everything_the_same_way()
    {
        var map = Draft("draft-nodes-small");

        Assert.Equal(WholeMapLayout.Place(map).Nodes, WholeMapLayout.Place(map).Nodes);
        Assert.Equal(WholeMapLayout.Place(map).Order, WholeMapLayout.Place(map).Order);
    }

    [Fact]
    public void Columns_run_by_the_edges_between_areas_with_the_unassigned_area_last()
    {
        var geometry = WholeMapLayout.Place(Draft("draft-nodes-small"));

        Assert.Equal(["feature:billing", "feature:tabs", "area:unassigned"], geometry.Order, StringComparer.Ordinal);
        Assert.Equal(geometry.Order, geometry.Columns.Select(c => c.Key), StringComparer.Ordinal);
        Assert.False(geometry.ByArea);
    }

    [Fact]
    public void Every_node_sits_inside_its_own_column_and_inside_the_world()
    {
        var map = Draft("draft-nodes-small");
        var geometry = WholeMapLayout.Place(map);

        Assert.Equal(map.Nodes!.Count, geometry.Nodes.Count);
        Assert.All(geometry.Nodes, node =>
        {
            var column = geometry.Columns[node.AreaIndex];
            Assert.Equal(map.Nodes.Single(n => n.Key == node.Key).Area, column.Key);
            Assert.InRange(node.Rect.X, column.Rect.X, column.Rect.Right);
            Assert.InRange(node.Rect.Right, column.Rect.X, column.Rect.Right + WholeMapLayout.ColumnGutter);
            Assert.InRange(node.Rect.Y, 0, geometry.World.Bottom);
            Assert.InRange(node.Rect.Right, 0, geometry.World.Right);
        });
    }

    [Fact]
    public void Decisions_sit_above_the_product_and_code_below_it_in_every_column()
    {
        var geometry = WholeMapLayout.Place(Draft("draft-nodes-small"));
        var decision = geometry.Nodes.Single(n => n.Kind == "decision");
        var surface = geometry.Nodes.First(n => n.Kind == "surface");
        var code = geometry.Nodes.First(n => n.Kind == "code");

        Assert.True(decision.Rect.Y < surface.Rect.Y);
        Assert.True(surface.Rect.Bottom <= code.Rect.Y);
        Assert.Equal(geometry.Bands.OrderBy(b => b.Top).Select(b => b.Band), geometry.Bands.Select(b => b.Band));
    }

    [Fact]
    public void Edges_join_the_centres_of_their_two_nodes_and_an_edge_to_a_missing_node_is_dropped()
    {
        var map = Draft("draft-nodes-small");
        var with = map with { Edges = [.. map.Edges!, new WholeEdge("feature:billing", "feature:gone", "part-of", "edge:x", "human", 1, Confirmed: true)] };

        var geometry = WholeMapLayout.Place(with);

        Assert.Equal(map.Edges!.Count, geometry.Edges.Count);
        var first = geometry.Edges[0];
        var from = geometry.Nodes.Single(n => n.Key == first.From);
        Assert.Equal((from.Rect.CenterX, from.Rect.CenterY), (first.X1, first.Y1));
    }

    [Fact]
    public void An_answer_by_area_is_one_tile_per_area_in_the_same_order_with_the_edges_between_them()
    {
        var map = Draft("draft-areas-over-budget");

        var geometry = WholeMapLayout.Place(map);

        Assert.True(geometry.ByArea);
        Assert.Equal(map.Areas.Count, geometry.Tiles.Count);
        Assert.Equal(geometry.Order, geometry.Tiles.Select(t => t.Key), StringComparer.Ordinal);
        Assert.Equal("area:unassigned", geometry.Tiles[^1].Key);
        Assert.Empty(geometry.Nodes);
        Assert.Equal(map.AreaEdges.Count, geometry.AreaEdges.Count);
        Assert.Equal(geometry.Tiles.Count, geometry.Tiles.Select(t => (t.Rect.X, t.Rect.Y)).Distinct().Count());
    }

    [Fact]
    public void Five_hundred_nodes_get_five_hundred_places_that_do_not_overlap()
    {
        var kinds = new[] { "decision", "surface", "code", "feature", "record" };
        var areas = Enumerable.Range(0, 10).Select(i => new WholeArea("feature:" + i.ToString("00", CultureInfo.InvariantCulture), "Feature " + i.ToString(CultureInfo.InvariantCulture), "trusted", 50, new Dictionary<string, int>(StringComparer.Ordinal), Unassigned: false)).ToList();
        var nodes = Enumerable.Range(0, 500).Select(i => new WholeNode("node:" + i.ToString("000", CultureInfo.InvariantCulture), "Node " + i.ToString(CultureInfo.InvariantCulture), kinds[i % kinds.Length], areas[i % areas.Count].Key, [], "trusted", "human", 1, Confirmed: true)).ToList();
        var map = Draft("draft-nodes-small") with { Areas = areas, AreaEdges = [], Nodes = nodes, Edges = [] };

        var geometry = WholeMapLayout.Place(map);

        Assert.Equal(500, geometry.Nodes.Count);
        var cells = geometry.Nodes.Select(n => (n.Rect.X, n.Rect.Y)).ToHashSet();
        Assert.Equal(500, cells.Count);
        Assert.All(geometry.Nodes, n => Assert.True(geometry.World.Contains(n.Rect.CenterX, n.Rect.CenterY)));
    }
}
