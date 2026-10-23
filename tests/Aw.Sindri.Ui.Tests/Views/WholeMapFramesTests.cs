using System.Globalization;
using System.Net;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Context.Whole;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The Context page opening on the whole map (whole-map spec §3.1) against the in-memory API serving the DRAFT whole-map goldens (Golden/SpecShaped/context-whole,
/// swapped for context's real ones when W1 lands): the map drawn in one canvas, the list of features, the first open's zoom, no sideways scroll at 900 px, the
/// by-feature overview, and the No map state over it. Saved under frames/ as whole-map-*.png when AW_UI_FRAMES=1.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class WholeMapFramesTests : FrameHost
{
    private static RouteAnswer Draft(string name) => new(HttpStatusCode.OK, GoldenFiles.SpecShaped("context-whole", name));

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Open(double width, double height, RouteAnswer? answer = null)
    {
        if (answer is { } served)
        {
            Server.WholeMapAnswer = _ => served;
        }

        var (window, shell) = await Connected(width: width, height: height);
        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowWhole && window.GetVisualDescendants().OfType<WholeMapView>().Any(v => v.Canvas.Bounds.Width > 0));
        Settle(window);
        return (window, shell);
    }

    [Fact]
    public Task The_page_opens_on_the_whole_map_with_every_feature_listed_and_the_first_zoom_in_range() => OnUi(async () =>
    {
        var (window, shell) = await Open(1440, 900);

        var whole = Named<WholeMapView>(window, "WholeMap");
        whole.Canvas.Settle();
        Assert.Equal(shell.Context.Whole.Geometry!.World.Width, whole.Canvas.WorldBounds.Width);
        Assert.InRange(whole.Canvas.View.Scale, WholeMapViewModel.FirstOpenFloor, WholeMapViewModel.MaxScale);
        Assert.Equal(shell.Context.Whole.Areas.Count, Named<ItemsControl>(window, "WholeAreas").ItemCount);
        Assert.Equal(shell.Context.Whole.Head, shell.Context.Head);
        Assert.False(Visible(window, "WholeStatus"));
        Assert.False(Visible(window, "ContextEmpty"));
        Frame(window, "whole-map-1440", "w3-");
    });

    [Fact]
    public Task At_900_the_page_does_not_scroll_sideways() => OnUi(async () =>
    {
        var (window, _) = await Open(900, 800);

        var pages = Named<ScrollViewer>(window, "Pages");
        Assert.True(pages.Extent.Width <= pages.Viewport.Width + 0.5, string.Create(CultureInfo.InvariantCulture, $"{pages.Extent.Width} > {pages.Viewport.Width}"));
        Frame(window, "whole-map-900", "w3-");
    });

    [Fact]
    public Task A_map_over_the_budget_opens_by_feature_with_its_one_line_and_a_click_on_a_feature_zooms_to_it() => OnUi(async () =>
    {
        var (window, shell) = await Open(1440, 900, Draft("draft-areas-over-budget"));

        Assert.True(shell.Context.Whole.ByArea);
        Assert.True(Visible(window, "WholeNote"));
        Assert.Equal(shell.Context.Whole.ByAreaNote, Named<TextBlock>(window, "WholeNote").Text);
        var whole = Named<WholeMapView>(window, "WholeMap");
        var before = whole.Canvas.View;

        shell.Context.Whole.Areas[0].Open();
        whole.Canvas.Settle();

        Assert.NotEqual(before, whole.Canvas.View);
        Frame(window, "whole-map-by-area-1440", "w3-");
    });

    [Fact]
    public Task No_map_shows_the_existing_empty_state_not_the_whole_map() => OnUi(async () =>
    {
        Server.WholeMapAnswer = _ => RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-030", "no map yet (fix: sindri context map build)");
        var (window, shell) = await Connected(width: 1440, height: 900);

        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowEmpty);
        Settle(window);

        Assert.True(Visible(window, "ContextEmpty"));
        Assert.False(Visible(window, "WholeMapHost"));
        Assert.False(Visible(window, "WholeStatus"));
    });

    [Fact]
    public Task A_failed_read_shows_the_failure_row_with_retry_that_reads_again() => OnUi(async () =>
    {
        var failing = true;
        Server.WholeMapAnswer = _ => failing ? new RouteAnswer(HttpStatusCode.InternalServerError, "{}") : Draft("draft-nodes-small");
        var (window, shell) = await Connected(width: 1440, height: 900);

        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.Whole.IsFailed);
        Settle(window);
        Assert.True(Visible(window, "WholeStatus"));
        Assert.True(Visible(window, "WholeRetry"));

        failing = false;
        Named<Button>(window, "WholeRetry").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.Context.ShowWhole);

        Assert.False(Visible(window, "WholeStatus"));
    });
}
