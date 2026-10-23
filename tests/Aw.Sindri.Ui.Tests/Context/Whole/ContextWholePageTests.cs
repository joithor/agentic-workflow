using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The Context page opens on the whole map (whole-map spec §3.1): read once when first shown with no starting point, retried on a failure, and covered by the No map state when there is no map. Goldens from #830 (Golden/Api/context-whole).</summary>
[Lane(TestLane.Unit)]
public sealed class ContextWholePageTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Anchor => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    private static RouteAnswer NoMap => RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-030", "no map yet (fix: sindri context map build)");

    private static async Task<(ContextPageViewModel Page, List<Func<Uri, CancellationToken, Task>> Follows)> Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var follows = new List<Func<Uri, CancellationToken, Task>>();
        return (new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, follows.Add)), follows);
    }

    [Fact]
    public async Task The_first_show_reads_the_whole_map_with_no_starting_point_and_draws_it()
    {
        var api = new FakeSindriApi();
        var (page, _) = await Page(api);

        await page.Opened(Token);

        var asked = Assert.Single(api.WholeMapAsked);
        Assert.Equal(("auto", null, false), (asked.Detail, asked.Area, asked.Draft));
        Assert.Empty(asked.Locate);
        Assert.Equal((true, true, false), (page.Whole.IsReady, page.ShowWhole, page.ShowWholeStatus));
        Assert.Equal(("Context · the whole map · v1", true), (page.Head, page.HasHead));
        Assert.Equal(0, api.ContextCalls);
    }

    [Fact]
    public async Task Showing_the_page_again_does_not_read_the_map_again()
    {
        var api = new FakeSindriApi();
        var (page, _) = await Page(api);

        await page.Opened(Token);
        await page.Opened(Token);

        _ = Assert.Single(api.WholeMapAsked);
    }

    [Fact]
    public async Task While_the_map_is_read_a_quiet_row_shows_and_the_stage_waits()
    {
        var gate = new TaskCompletionSource();
        var api = new FakeSindriApi { WholeMapGate = gate };
        var (page, _) = await Page(api);

        var reading = page.Opened(Token);

        Assert.Equal((false, true, true), (page.ShowWhole, page.ShowWholeStatus, page.Whole.IsLoading));
        _ = gate.TrySetResult();
        await reading;
        Assert.Equal((true, false), (page.ShowWhole, page.ShowWholeStatus));
    }

    [Fact]
    public async Task A_failed_read_says_so_with_retry_and_a_retry_reads_again()
    {
        var failing = true;
        var api = new FakeSindriApi { WholeMapAnswer = q => failing ? new RouteAnswer(HttpStatusCode.InternalServerError, "{}") : new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("context-whole", "nodes-small")) };
        var (page, _) = await Page(api);

        await page.Opened(Token);
        Assert.Equal((true, false, true, false), (page.Whole.IsFailed, page.ShowWhole, page.ShowWholeStatus, page.Whole.IsReady));

        failing = false;
        await page.ReadWhole(Token);

        Assert.Equal((false, true, false), (page.Whole.IsFailed, page.ShowWhole, page.ShowWholeStatus));
        Assert.Equal(2, api.WholeMapAsked.Count);
    }

    [Fact]
    public async Task A_map_over_the_budget_opens_by_feature()
    {
        var api = new FakeSindriApi { WholeMapAnswer = _ => new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("context-whole", "areas-over-budget")) };
        var (page, _) = await Page(api);

        await page.Opened(Token);

        Assert.True(page.Whole.ByArea);
        Assert.True(page.ShowWhole);
    }

    [Fact]
    public async Task No_map_is_the_existing_empty_state_and_the_whole_map_stays_off()
    {
        var api = new FakeSindriApi { WholeMapAnswer = _ => NoMap };
        var (page, _) = await Page(api);

        await page.Opened(Token);

        Assert.Equal((ContextOverlay.Empty, false, false, false), (page.Overlay, page.ShowWhole, page.ShowWholeStatus, page.Whole.IsFailed));
        Assert.False(page.HasProblem);
    }

    [Fact]
    public async Task A_query_no_longer_replaces_the_whole_map_it_zooms_into_it()
    {
        var api = new FakeSindriApi();
        var (page, _) = await Page(api);
        await page.Opened(Token);
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Equal((true, false, "Context · the whole map · v1"), (page.ShowWhole, page.MapVisible, page.Head));
    }

    [Fact]
    public async Task A_refused_query_leaves_the_whole_map_and_its_head_where_they_were()
    {
        var api = new FakeSindriApi { LensesAnswer = _ => RouteAnswer.Problem("context-lenses", "unknown-anchor-003") };
        var (page, _) = await Page(api);
        await page.Opened(Token);
        page.AnchorText = "feature:nope";

        await page.Submit(Token);

        Assert.Equal((true, "Context · the whole map · v1", true), (page.ShowWhole, page.Head, page.HasProblem));
    }

    [Fact]
    public async Task A_build_that_ends_reads_the_whole_map_again()
    {
        var map = new[] { NoMap };
        var api = new FakeSindriApi { WholeMapAnswer = _ => map[0] };
        var (page, follows) = await Page(api);
        await page.Opened(Token);
        Assert.True(page.NoMap);

        map[0] = new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("context-whole", "nodes-small"));
        await page.ReadWhole(Token);
        foreach (var follow in follows)
        {
            await follow(new Uri("http://127.0.0.1:1/"), Token);
        }

        Assert.True(page.Whole.IsReady);
        Assert.Equal(2, api.WholeMapAsked.Count);
    }


    [Fact]
    public async Task A_tile_click_reads_that_feature_in_full_with_area_and_draws_it()
    {
        var api = new FakeSindriApi { WholeMapAnswer = q => q.Area is null ? RouteAnswer.Api("context-whole", "areas-over-budget") : ContextAnswers.WholeMap(q) };
        var (page, _) = await Page(api);
        await page.Opened(Token);

        page.Whole.Areas.Single(a => a.Key == ContextAnswers.OpenArea).Open();
        await Until(() => page.Whole.IsAreaOpen);

        Assert.Equal(ContextAnswers.OpenArea, api.WholeMapAsked[^1].Area);
        Assert.Equal((true, false, true), (page.Whole.IsAreaOpen, page.HasProblem, page.ShowWhole));
    }

    [Fact]
    public async Task An_area_the_map_does_not_have_is_the_existing_banner_and_the_overview_stays()
    {
        var api = new FakeSindriApi { WholeMapAnswer = q => q.Area is null ? RouteAnswer.Api("context-whole", "areas-over-budget") : RouteAnswer.Problem("context-whole", "area-unknown-200") };
        var (page, _) = await Page(api);
        await page.Opened(Token);

        await page.OpenWholeArea("feature:nope", Token);

        Assert.Equal((true, false, true), (page.HasProblem, page.Whole.IsAreaOpen, page.ShowWhole));
        Assert.Contains("isn't in this map", page.Problem, StringComparison.Ordinal);
    }

    private static async Task Until(Func<bool> done)
    {
        for (var i = 0; i < 200 && !done(); i++)
        {
            await Task.Delay(10, Token);
        }
    }
}
