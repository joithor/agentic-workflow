using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Views.Controls;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The row of feature chips over the whole map is one line with a "+N more" chip, so a repository with many features still shows the graph
/// (Context stage; whole-map spec §3.1). The existing frames use two features, which is why a wrapping row stayed green.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ContextGraphLayoutTests : FrameHost
{
    private const double GraphFloor = 240;
    private const double OneLine = 48;

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(900, 800)]
    public Task A_long_feature_list_is_one_line_with_the_right_more_count_and_the_graph_stays_visible(int width, int height) => OnUi(async () =>
    {
        var (window, shell) = await Open(ManyAreas(240), width, height);

        var (panel, more) = Row(window);

        var shown = panel.Children.Count(c => c.IsVisible);
        var total = shell.Context.Whole.Chips.Count;
        Assert.True(shown > 0 && shown < total, string.Create(CultureInfo.InvariantCulture, $"{shown} of {total} chips on the line"));
        Assert.Equal(shown, panel.VisibleCount);
        Assert.Equal((true, string.Create(CultureInfo.InvariantCulture, $"+{total - shown} more")), (more.IsVisible, more.Content));
        Assert.All(panel.Children.Where(c => c.IsVisible), c => Assert.Equal(panel.Children.First(v => v.IsVisible).Bounds.Y, c.Bounds.Y));
        Assert.True(panel.Bounds.Height < OneLine, string.Create(CultureInfo.InvariantCulture, $"the row is {panel.Bounds.Height} high"));
        var canvas = Named<ZoomBorder>(window, "WholeCanvas");
        Assert.True(canvas.Bounds.Height >= GraphFloor, string.Create(CultureInfo.InvariantCulture, $"graph {canvas.Bounds.Height}"));
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"whole-map-chips-{width}"), "w5-");
    });

    [Fact]
    public Task The_more_chip_shows_the_rest_of_the_features() => OnUi(async () =>
    {
        var (window, shell) = await Open(ManyAreas(240), 1440, 900);
        var (panel, more) = Row(window);
        var hidden = shell.Context.Whole.Chips.Count - panel.VisibleCount;

        more.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        var popup = Named<Avalonia.Controls.Primitives.Popup>(window, "WholeMoreChipsPopup");
        Assert.Equal((true, hidden), (popup.IsOpen, Named<ItemsControl>(window, "WholeHiddenChips").ItemCount));
        Frame(window, "whole-map-chips-more", "w5-");
    });

    [Fact]
    public Task A_few_features_all_fit_the_line_and_have_no_more_chip() => OnUi(async () =>
    {
        var (window, shell) = await Open(body: null, 1440, 900);

        var (panel, more) = Row(window);

        Assert.Equal((shell.Context.Whole.Chips.Count, false), (panel.VisibleCount, more.IsVisible));
    });

    private async Task<(ShellWindow Window, ViewModels.Shell.ShellViewModel Shell)> Open(string? body, int width, int height)
    {
        if (body is not null)
        {
            Server.WholeMapAnswer = _ => new RouteAnswer(HttpStatusCode.OK, body);
        }

        var (window, shell) = await Connected(width: width, height: height);
        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowWhole && shell.Context.Whole.Chips.Count > 0 && window.GetVisualDescendants().OfType<ZoomBorder>().Any(c => c.Name == "WholeCanvas" && c.Bounds.Height > 0));
        Settle(window);
        return (window, shell);
    }

    private static (OneLinePanel Panel, Button More) Row(ShellWindow window)
    {
        var panels = window.GetVisualDescendants().OfType<OneLinePanel>().ToList();
        var buttons = window.GetVisualDescendants().OfType<Button>().Where(b => b.Name == "WholeMoreChips").ToList();
        Assert.True(panels.Count == 1 && buttons.Count == 1, string.Create(CultureInfo.InvariantCulture, $"{panels.Count} rows, {buttons.Count} more chips"));
        return (panels[0], buttons[0]);
    }

    // The over-budget golden has two features. Copies of the linked one stand in for a repository this screen cannot scroll.
    private static string ManyAreas(int extra)
    {
        var node = GoldenFiles.Node("context-whole", "areas-over-budget");
        var areas = node["areas"]!.AsArray();
        var sample = areas[1]!.ToJsonString();
        for (var i = 0; i < extra; i++)
        {
            var copy = JsonNode.Parse(sample)!;
            copy["key"] = string.Create(CultureInfo.InvariantCulture, $"feature:extra.{i:000}");
            copy["label"] = string.Create(CultureInfo.InvariantCulture, $"Feature {i:000} scheduling");
            copy["unassigned"] = false;
            areas.Add(copy);
        }

        return node.ToJsonString();
    }
}
