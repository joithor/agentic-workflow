using Avalonia;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.Views.Context.Whole;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The mini-map sits over the stage's bottom-left corner; a fitted map must never have a card under it (q86, #885's frames at 900x800).</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapMiniMapTests
{
    private const double StageWidth = 900;
    private const double StageHeight = 800;

    private static Rect OnScreen(WholeRect rect, ZoomView view) =>
        new((rect.X * view.Scale) + view.X, (rect.Y * view.Scale) + view.Y, rect.Width * view.Scale, rect.Height * view.Scale);

    private static void AssertClear(WholeMapGeometry geometry)
    {
        var canvas = new Size(StageWidth, StageHeight - WholeMapStage.ReservedBottom);
        var view = ZoomView.Fit(canvas, new Rect(geometry.World.X, geometry.World.Y, geometry.World.Width, geometry.World.Height), new ZoomRange(WholeMapViewModel.FirstOpenFloor, WholeMapViewModel.MaxScale));
        var miniMap = WholeMapStage.MiniMapRect(StageHeight);
        var cards = geometry.Tiles.Select(t => t.Rect).Concat(geometry.Nodes.Select(n => n.Rect)).Concat(geometry.Columns.Select(c => c.Rect)).ToList();

        Assert.NotEmpty(cards);
        Assert.All(cards, card => Assert.False(OnScreen(card, view).Intersects(miniMap), $"a card at {card} sits under the mini-map"));
    }

    [Fact]
    public void No_card_of_the_over_budget_overview_sits_under_the_minimap_at_900_by_800() =>
        AssertClear(WholeMapLayout.Place(WholeMapLayoutTests.Draft("areas-over-budget")));

    [Fact]
    public void No_card_of_a_node_level_map_sits_under_the_minimap_at_900_by_800() =>
        AssertClear(WholeMapLayout.Place(WholeMapLayoutTests.Draft("nodes-small")));

    [Fact]
    public void The_canvas_ends_where_the_minimap_starts()
    {
        var miniMap = WholeMapStage.MiniMapRect(StageHeight);

        Assert.Equal(StageHeight - WholeMapStage.ReservedBottom, miniMap.Y - WholeMapStage.MiniMapMargin);
    }
}
