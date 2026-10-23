using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The whole map's view model (whole-map spec §3.1, §3.2): its head and notes, the list of features, the zoom thresholds. DRAFT goldens, see <see cref="WholeMapLayoutTests"/>.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapViewModelTests
{
    private static readonly TimeZoneInfo _utc = TimeZoneInfo.Utc;

    [Fact]
    public void Before_an_answer_there_is_nothing_ready_and_nothing_drawn()
    {
        var map = new WholeMapViewModel(() => _utc);

        Assert.Equal((false, false, false, string.Empty), (map.IsReady, map.IsLoading, map.IsFailed, map.Head));
        Assert.Null(map.Geometry);
    }

    [Fact]
    public void A_node_level_answer_reads_the_head_with_the_version_and_the_size_and_age()
    {
        var map = new WholeMapViewModel(() => _utc);

        map.Show(WholeMapLayoutTests.Draft("draft-nodes-small"));

        Assert.Equal(("Context · the whole map · v3", false, false), (map.Head, map.ByArea, map.HasNote));
        Assert.Equal("7 items · 7 links · built 23 Oct 2026 06:00", map.SizeText);
        Assert.Equal((true, false, false), (map.IsReady, map.IsLoading, map.IsFailed));
    }

    [Fact]
    public void An_answer_by_area_says_in_one_line_why_it_is_grouped_by_feature()
    {
        var map = new WholeMapViewModel(() => _utc);
        var answer = WholeMapLayoutTests.Draft("draft-areas-over-budget");

        map.Show(answer);

        Assert.True(map.ByArea);
        Assert.Equal(Labels.ContextWholeByArea(answer.Counts.Nodes), map.ByAreaNote);
        Assert.Contains("grouped by feature", map.ByAreaNote, StringComparison.Ordinal);
        Assert.DoesNotContain("area", map.ByAreaNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_list_of_features_follows_the_map_order_and_each_names_its_size()
    {
        var map = new WholeMapViewModel(() => _utc);
        var answer = WholeMapLayoutTests.Draft("draft-nodes-small");

        map.Show(answer);

        Assert.Equal(map.Geometry!.Order, map.Areas.Select(a => a.Key), StringComparer.Ordinal);
        var billing = map.Areas.Single(a => a.Key == "feature:billing");
        Assert.Equal(("Billing", "3 items", "Billing, 3 items. Press Enter to open."), (billing.Label, billing.MembersText, billing.Name));
        Assert.Equal("1 code file, 1 feature, 1 screen", billing.KindsText);
    }

    [Fact]
    public void Opening_a_feature_asks_the_camera_for_its_tile_until_the_route_that_opens_one_exists()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("draft-areas-over-budget"));
        var asked = new List<WholeRect>();
        map.ZoomRequested += (_, e) => asked.Add(e.Value);
        var tile = map.Geometry!.Tiles[1];

        map.Areas[1].Open();

        Assert.Equal([tile.Rect], asked);
    }

    [Fact]
    public void A_node_click_is_raised_with_its_key()
    {
        var map = new WholeMapViewModel(() => _utc);
        var chosen = new List<string>();
        map.NodeChosen += (_, e) => chosen.Add(e.Value);

        map.ChooseNode("feature:tabs");

        Assert.Equal(["feature:tabs"], chosen);
    }

    [Fact]
    public void A_failed_read_keeps_what_was_drawn_and_a_new_answer_clears_the_failure()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("draft-nodes-small"));

        map.Loading();
        Assert.Equal((true, true, false), (map.IsReady, map.IsLoading, map.IsFailed));
        map.Failed();
        Assert.Equal((true, false, true), (map.IsReady, map.IsLoading, map.IsFailed));
        Assert.NotNull(map.Geometry);

        map.Show(WholeMapLayoutTests.Draft("draft-nodes-small"));
        Assert.Equal((true, false, false), (map.IsReady, map.IsLoading, map.IsFailed));
    }

    [Theory]
    [InlineData(0.2, 0.25)]
    [InlineData(0.4, 0.4)]
    [InlineData(3.0, 1.0)]
    public void The_first_open_fits_the_map_but_never_below_the_floor(double fit, double expected) =>
        Assert.Equal(expected, WholeMapViewModel.FirstOpenScale(fit));

    [Theory]
    [InlineData(0.34, true)]
    [InlineData(0.35, false)]
    [InlineData(1.0, false)]
    public void Nodes_are_dots_below_about_a_third_of_the_size(double scale, bool dots) =>
        Assert.Equal(dots, WholeMapViewModel.UsesDots(scale));

    [Theory]
    [InlineData(0, 0, "Solid")]
    [InlineData(5, 5, "Solid")]
    [InlineData(6, 0, "Hatched")]
    [InlineData(12, 0, "Ringed")]
    [InlineData(18, 0, "Solid")]
    public void More_areas_than_colours_reuse_a_colour_with_the_next_pattern(int index, int colour, string pattern)
    {
        var (c, p) = WholePalette.For(index);

        Assert.Equal((colour, pattern), (c, p.ToString()));
    }
}
