using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Rendering.Tests.Capture;

[Lane(TestLane.Unit)]
public sealed class CaptureArgumentsTests
{
    private static CaptureRequest? Parse(params string[] args) => CaptureArguments.Parse(["--capture", .. args], static _ => { });

    [Fact]
    public void Capture_mode_is_asked_for_by_the_first_argument_only()
    {
        Assert.True(CaptureArguments.Asked(["--capture", "--size", "1x1"]));
        Assert.False(CaptureArguments.Asked(["sindri://item/ITM-1"]));
        Assert.False(CaptureArguments.Asked([]));
    }

    [Fact]
    public void A_size_and_two_shots_are_read_in_order()
    {
        var parsed = Parse("--size", "1440x900", "--shot", "page:Home", "/o/a.png", "--shot", "item/ITM-1", "/o/b.png");

        Assert.Equal((1440, 900), (parsed!.Width, parsed.Height));
        Assert.Equal(["page:Home=/o/a.png", "item/ITM-1=/o/b.png"], parsed.Shots.Select(static s => s.Route + "=" + s.Path));
    }

    [Theory]
    [InlineData("--shot", "page:Home", "/o/a.png")]
    [InlineData("--size", "1440x900")]
    [InlineData("--size", "big", "--shot", "page:Home", "/o/a.png")]
    [InlineData("--size", "0x900", "--shot", "page:Home", "/o/a.png")]
    [InlineData("--size", "1x1", "--size", "2x2", "--shot", "page:Home", "/o/a.png")]
    [InlineData("--size", "1x1", "--shot", "page:Home")]
    [InlineData("--size", "1x1", "--other", "x", "--shot", "page:Home", "/o/a.png")]
    public void A_usage_mistake_is_not_parsed(params string[] args) => Assert.Null(Parse(args));

    [Fact]
    public void A_route_is_a_page_or_a_deep_link_and_nothing_else()
    {
        Assert.Equal(PageId.Workflows, Assert.IsType<CaptureRoute.OnPage>(CaptureRoute.Parse("page:Workflows")).Page);
        Assert.Equal(new DeepLink(DeepLinkKind.Item, "ITM-01JE7"), Assert.IsType<CaptureRoute.OnLink>(CaptureRoute.Parse("item/ITM-01JE7")).Link);
        Assert.Null(CaptureRoute.Parse("page:Nowhere"));
        Assert.Null(CaptureRoute.Parse("page:workflows"));
        Assert.Null(CaptureRoute.Parse("somewhere else"));
    }
}
