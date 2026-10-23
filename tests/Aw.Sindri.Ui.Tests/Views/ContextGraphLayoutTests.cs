using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// A repository with many features still shows the graph (Context stage). The chip row is an unbounded wrap inside a fixed-height
/// stage that clips, so the chips take the viewport and the canvas is left with nothing; the existing frames use two features, which
/// is why they stay green.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ContextGraphLayoutTests : FrameHost
{
    private const double GraphFloor = 240;

    [Fact]
    public Task A_long_feature_list_keeps_the_graph_visible_and_scrolls_within_the_list() => OnUi(async () =>
    {
        var body = ManyAreas(240);
        Server.WholeMapAnswer = _ => new RouteAnswer(HttpStatusCode.OK, body);
        var (window, shell) = await Connected(width: 1440, height: 900);

        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowWhole && shell.Context.Whole.Areas.Count > 100);
        Settle(window);

        var canvas = Named<ZoomBorder>(window, "WholeCanvas");
        var chips = window.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(c => c.Name == "WholeAreasScroll");
        var chipText = chips is null ? "missing" : string.Create(CultureInfo.InvariantCulture, $"{chips.Extent.Height}/{chips.Viewport.Height}");
        Assert.True(
            canvas.Bounds.Height >= GraphFloor && chips is not null && chips.Extent.Height > chips.Viewport.Height + 1 && chips.Viewport.Height <= 128,
            string.Create(CultureInfo.InvariantCulture, $"graph {canvas.Bounds.Height}; chips {chipText}"));
    });

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
