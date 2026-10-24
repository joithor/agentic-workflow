using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The box the camera eases to for a query's answer (whole-map spec §3.1 "Zoomed in").</summary>
[Lane(TestLane.Unit)]
public sealed class WholeZoomBoxTests
{
    [Fact]
    public void One_node_gets_the_minimum_box_centred_on_it()
    {
        var node = new WholeRect(1000, 500, 120, 28);

        var box = WholeZoomBox.Around([node]);

        Assert.Equal((WholeZoomBox.MinSize, WholeZoomBox.MinSize), (box.Width, box.Height));
        Assert.Equal((node.CenterX, node.CenterY), (box.CenterX, box.CenterY));
    }

    [Fact]
    public void Many_nodes_get_their_union_with_padding_all_round()
    {
        var box = WholeZoomBox.Around([new WholeRect(100, 100, 120, 28), new WholeRect(900, 700, 120, 28)]);

        Assert.Equal((100 - WholeZoomBox.Padding, 100 - WholeZoomBox.Padding), (box.X, box.Y));
        Assert.Equal((1020 + WholeZoomBox.Padding, 728 + WholeZoomBox.Padding), (box.Right, box.Bottom));
    }

    [Fact]
    public void No_nodes_is_no_box() => Assert.Equal(default, WholeZoomBox.Around([]));
}
