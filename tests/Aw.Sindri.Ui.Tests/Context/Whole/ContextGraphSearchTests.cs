using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>
/// Search on the whole map keeps only the answer (whole-map spec §3.1, as the Context screen is used): feature chips, nodes and
/// links outside the answer go, and "Whole map" puts them back. The zoom tests still allow the rest of the map to stay, dimmed,
/// which is why a large repo keeps every chip and every node after a search.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ContextGraphSearchTests
{
    private static readonly TimeZoneInfo _utc = TimeZoneInfo.Utc;

    [Fact]
    public void Search_drops_unrelated_nodes_links_and_feature_chips_and_whole_map_restores_them()
    {
        var map = new WholeMapViewModel(() => _utc);
        var answer = WholeMapLayoutTests.Draft("nodes-small");
        map.Show(answer);
        var nodes = map.Geometry!.Nodes.Count;
        var areas = map.Areas.Select(a => a.Key).ToList();
        var hit = answer.Nodes!.First(n => n.Area == "feature:tabs.tab-groups").Key;
        var unrelated = answer.Nodes!.First(n => n.Area == "area:unassigned").Key;

        Assert.True(map.Zoom([hit]));

        Assert.Equal([hit], map.Geometry!.Nodes.Select(n => n.Key).ToList());
        Assert.DoesNotContain(map.Geometry.Edges, e => e.From == unrelated || e.To == unrelated);
        Assert.All(map.Geometry.Edges, e => Assert.Equal((hit, hit), (e.From, e.To)));
        Assert.Equal(["feature:tabs.tab-groups"], map.Areas.Select(a => a.Key).ToList());
        map.Unzoom();
        Assert.Equal(nodes, map.Geometry!.Nodes.Count);
        Assert.Equal(areas, [.. map.Areas.Select(a => a.Key)]);
    }

    [Fact]
    public void Search_by_feature_drops_unrelated_chips_nodes_and_tiles_and_whole_map_restores_them()
    {
        var map = new WholeMapViewModel(() => _utc);
        var overview = WholeMapLayoutTests.Draft("areas-over-budget");
        map.Show(overview);
        var opened = WholeMapLayoutTests.Draft("area-open");
        var key = opened.Nodes![0].Key;
        var areas = map.Areas.Select(a => a.Key).ToList();

        Assert.True(map.ZoomIntoAreas(overview, [opened], [key], notOpened: 2));

        Assert.Equal([opened.Area!.Key], map.Areas.Select(a => a.Key).ToList());
        Assert.Equal([key], map.Geometry!.Nodes.Select(n => n.Key).ToList());
        Assert.Empty(map.Geometry.Tiles);
        Assert.All(map.Geometry.Edges, e => Assert.Equal((key, key), (e.From, e.To)));
        map.Unzoom();
        Assert.Equal(areas, [.. map.Areas.Select(a => a.Key)]);
        Assert.Equal((0, overview.Areas.Count), (map.Geometry!.Nodes.Count, map.Geometry.Tiles.Count));
    }
}
