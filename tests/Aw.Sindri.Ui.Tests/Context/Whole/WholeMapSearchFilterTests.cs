using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>A search filters the whole map to its matches and their direct links (owner change, reverses "rest dimmed"): the rest is not drawn and cannot be hit, and clearing the search brings the whole map back.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapSearchFilterTests
{
    private static readonly TimeZoneInfo _utc = TimeZoneInfo.Utc;

    private static WholeMapViewModel Show(string name)
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft(name));
        return map;
    }

    private static MapSearchHit Hit(string key) => new(key, "feature", key, Area: null);

    private static HashSet<string> WithLinks(WholeMapGeometry geometry, string key)
    {
        var keep = new HashSet<string>(StringComparer.Ordinal) { key };
        foreach (var edge in geometry.Edges.Where(e => string.Equals(e.From, key, StringComparison.Ordinal) || string.Equals(e.To, key, StringComparison.Ordinal)))
        {
            _ = keep.Add(edge.From);
            _ = keep.Add(edge.To);
        }

        return keep;
    }

    [Fact]
    public void Typing_a_query_draws_only_the_matches_and_their_direct_links()
    {
        var map = Show("nodes-small");
        var geometry = map.Geometry!;
        var match = geometry.Edges[0].From;
        var keep = WithLinks(geometry, match);

        map.ApplySearch([Hit(match)], hitAreas: [], zoomed: false);

        var shown = map.Shown!;
        Assert.Equal(keep.Order(StringComparer.Ordinal), shown.Nodes.Select(n => n.Key).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(geometry.Edges.Count(e => keep.Contains(e.From) && keep.Contains(e.To)), shown.Edges.Count);
        Assert.All(shown.Edges, e => Assert.True(keep.Contains(e.From) && keep.Contains(e.To)));
        Assert.True(shown.Nodes.Count < geometry.Nodes.Count);
    }

    [Fact]
    public void Typing_a_query_draws_only_the_matching_tiles_on_the_overview()
    {
        var map = Show("areas-over-budget");
        var tile = map.Geometry!.Tiles[0];

        map.ApplySearch([Hit(tile.Key)], hitAreas: [], zoomed: false);

        var shown = map.Shown!;
        Assert.Contains(tile.Key, shown.Tiles.Select(t => t.Key), StringComparer.Ordinal);
        Assert.True(shown.Tiles.Count < map.Geometry.Tiles.Count);
        Assert.All(shown.AreaEdges, e => Assert.True(e.From == tile.Key || e.To == tile.Key || (shown.Tiles.Any(t => t.Key == e.From) && shown.Tiles.Any(t => t.Key == e.To))));
    }

    [Fact]
    public void Clearing_the_query_restores_the_whole_map()
    {
        var map = Show("nodes-small");
        map.ApplySearch([Hit(map.Geometry!.Edges[0].From)], hitAreas: [], zoomed: false);

        map.EndSearch();

        Assert.Same(map.Geometry, map.Shown);
        Assert.Equal(map.Geometry.World, map.FitBox);
    }

    [Fact]
    public void A_search_with_no_matches_draws_nothing()
    {
        var map = Show("nodes-small");

        map.ApplySearch([], hitAreas: [], zoomed: false);

        Assert.Empty(map.Shown!.Nodes);
        Assert.Empty(map.Shown.Edges);
        Assert.Equal(map.Geometry!.World, map.FitBox);
    }

    [Fact]
    public void The_minimap_and_fit_cover_the_filtered_set()
    {
        var map = Show("nodes-small");
        var match = map.Geometry!.Edges[0].From;

        map.ApplySearch([Hit(match)], hitAreas: [], zoomed: false);

        Assert.Equal(WholeZoomBox.Around([.. map.Shown!.Nodes.Select(n => n.Rect)]), map.FitBox);
        Assert.NotEqual(map.Geometry.World, map.FitBox);
    }

    [Fact]
    public void The_filter_announces_itself_when_it_starts_and_when_it_ends()
    {
        var map = Show("nodes-small");
        var changed = new List<string?>();
        map.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        map.ApplySearch([Hit(map.Geometry!.Edges[0].From)], hitAreas: [], zoomed: false);
        map.EndSearch();

        Assert.Equal(2, changed.Count(n => string.Equals(n, nameof(WholeMapViewModel.Shown), StringComparison.Ordinal)));
    }

    [Fact]
    public void Hover_and_pin_work_on_the_filtered_set()
    {
        var map = Show("nodes-small");
        var geometry = map.Geometry!;
        var match = geometry.Edges[0].From;
        var keep = WithLinks(geometry, match);
        map.ApplySearch([Hit(match)], hitAreas: [], zoomed: false);
        var kept = map.Shown!.Nodes[0];
        var gone = geometry.Nodes.First(n => !keep.Contains(n.Key));

        map.PointerMoved(kept.Rect.CenterX, kept.Rect.CenterY);
        var hovered = (map.Focus, map.Emphasis!.All(keep.Contains));
        map.PointerMoved(gone.Rect.CenterX, gone.Rect.CenterY);
        var overGone = map.Focus;
        map.PointerClicked(kept.Rect.CenterX, kept.Rect.CenterY);

        Assert.Equal((kept.Key, true), hovered);
        Assert.Null(overGone);
        Assert.Equal(kept.Key, map.Pinned);
        Assert.Null(map.KeyAt(gone.Rect.CenterX, gone.Rect.CenterY));
    }

    [Fact]
    public void A_query_zoom_draws_only_the_hits_and_their_direct_links()
    {
        var map = Show("nodes-small");
        var geometry = map.Geometry!;
        var hit = geometry.Edges[0].From;
        var keep = WithLinks(geometry, hit);

        var zoomed = map.Zoom([hit]);

        var shown = map.Shown!;
        Assert.True(zoomed);
        Assert.Equal(keep.Order(StringComparer.Ordinal), shown.Nodes.Select(n => n.Key).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(shown.Edges, e => Assert.True(keep.Contains(e.From) && keep.Contains(e.To)));
        Assert.Equal(WholeZoomBox.Around([.. shown.Nodes.Select(n => n.Rect)]), map.FitBox);

        map.Unzoom();

        Assert.Same(map.Geometry, map.Shown);
        Assert.Equal(map.Geometry!.World, map.FitBox);
    }
}
