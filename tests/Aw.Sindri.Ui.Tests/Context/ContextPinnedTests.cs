using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The "As of" choice (F3 context spec 1 row 1b, §9 item 1): Approval on reads the map version the item's scope was frozen at; Live reads the current one; a purged version falls back to live in words.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextPinnedTests
{
    private const string Item = "ITM-0000000000000000000000000000000f";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    private static string AnchorKey => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    // The scope-found golden with its starting point and map version swapped for the ones the fake's lenses know ("v1").
    private static RouteAnswer Frozen(string version = "v1")
    {
        var golden = GoldenFiles.Node("items-id-scope", "scope-found");
        var scope = golden["scope"]!;
        scope["anchors"] = new JsonArray(new JsonObject { ["key"] = AnchorKey, ["mapVersion"] = version });
        scope["mapVersion"] = version;
        return new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
    }

    [Fact]
    public async Task Without_a_frozen_scope_only_live_is_offered_and_the_pinned_choice_says_why()
    {
        var api = new FakeSindriApi();
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.Equal([Item], api.ScopeAsked);
        Assert.Equal((false, false, true, Labels.ContextMapPinnedNotServed), (page.PinnedEnabled, page.IsPinned, page.IsLive, page.AsOfPinnedHint));
        Assert.Equal(string.Empty, page.AnchorText);
    }

    [Fact]
    public async Task A_frozen_scope_fills_the_starting_points_and_offers_approval_on_its_date()
    {
        var api = new FakeSindriApi { ItemScopeAnswer = _ => Frozen() };
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.Equal(AnchorKey, page.AnchorText);
        Assert.Equal((true, "Approval on 1 Jan 2026", true), (page.PinnedEnabled, page.AsOfPinned, page.IsLive));
        Assert.Equal(Labels.ContextHead(1), page.Head);   // read live first
    }

    [Fact]
    public async Task Choosing_approval_on_reads_the_frozen_map_version_and_live_reads_the_current_one()
    {
        var asked = new List<string?>();
        var api = new FakeSindriApi { ItemScopeAnswer = _ => Frozen() };
        var inner = api.LensesAnswer;
        api.LensesAnswer = query =>
        {
            asked.Add(query.MapVersion);
            return inner(query);
        };
        var page = Page(api);
        await page.OpenForItem(Item, Token);

        await page.Choose(pinned: true, Token);
        await page.Choose(pinned: false, Token);

        Assert.Equal([null, "v1", null], asked);
        Assert.Equal((false, true), (page.IsPinned, page.IsLive));
    }

    [Fact]
    public async Task A_version_that_is_no_longer_kept_falls_back_to_the_live_map_and_says_so()
    {
        var api = new FakeSindriApi { ItemScopeAnswer = _ => Frozen("v7") };
        var page = Page(api);
        await page.OpenForItem(Item, Token);

        await page.Choose(pinned: true, Token);

        Assert.Equal(("That version of the map is no longer kept. Showing the live map.", false, true, false), (page.PinnedNote, page.IsPinned, page.HasPinnedNote, page.PinnedEnabled));
        Assert.Equal(Labels.ContextHead(1), page.Head);
        Assert.False(page.HasProblem);
    }

    [Fact]
    public async Task Starting_points_read_at_different_times_leave_only_live_and_name_the_mix()
    {
        var api = new FakeSindriApi { ItemScopeAnswer = _ => RouteAnswer.Api("items-id-scope", "scope-mixed-versions") };
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.Equal((false, "These starting points were read at different times."), (page.PinnedEnabled, page.AsOfPinnedHint));
        await page.Choose(pinned: true, Token);
        Assert.True(page.IsLive);
    }

    [Fact]
    public async Task An_unknown_item_leaves_the_page_as_it_was()
    {
        var api = new FakeSindriApi { ItemScopeAnswer = _ => RouteAnswer.Problem("items-id-scope", "not-found") };
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.Equal((false, true, string.Empty), (page.PinnedEnabled, page.IsLive, page.AnchorText));
    }
}
