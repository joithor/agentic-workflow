using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>
/// The item detail sheet (UI spec §4.6, §4.9 Item detail) over GET /sindri/items/{id} and its timeline, with Close item
/// (CloseItem:done / :cancelled, desk, E-8) sending the version the human saw (§4.5, §9.1).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ItemSheetViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.SpecShaped("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static void IsObservable(object list) => _ = Assert.IsType<System.Collections.Specialized.INotifyCollectionChanged>(list, exactMatch: false);

    private async Task<ItemSheetViewModel> Sheet()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        return new ItemSheetViewModel(_server, catalog, _clock);
    }

    [Fact]
    public async Task Opening_reads_the_item_and_the_first_page_of_its_timeline()
    {
        var sheet = await Sheet();

        await sheet.Open(_api, A.NewItem, Token);

        Assert.Equal((A.NewItem, "Try the queue", "agentic-workflow", "New", "version 1", "5m"), (sheet.ItemId, sheet.Title, sheet.Repo, sheet.StateWord, sheet.VersionText, sheet.Age));
        Assert.Equal(("human:cli", false, false), (sheet.CreatedBy, sheet.HasWaitingOn, sheet.IsNotFound));
        Assert.Equal(["item.created", "item.linked"], sheet.Timeline.Take(2).Select(e => e.Type), StringComparer.Ordinal);
        Assert.Equal(100, sheet.Timeline.Count);
        Assert.Equal(("11:55:00", "human:cli", "Created “Try the queue” in agentic-workflow"), (sheet.Timeline[0].Time, sheet.Timeline[0].Actor, sheet.Timeline[0].Line));
        Assert.True(sheet.HasMore);
    }

    // L1: the timeline is patched by sequence, so a refresh keeps the rows (and the focus on one) as they were.
    [Fact]
    public async Task A_refresh_keeps_the_timeline_rows_it_already_shows()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        IsObservable(sheet.Timeline);
        var (timeline, second) = (sheet.Timeline, sheet.Timeline[1]);

        await sheet.Refresh(_api, Token);

        Assert.Same(timeline, sheet.Timeline);
        Assert.Same(second, sheet.Timeline[1]);
        Assert.Equal(100, sheet.Timeline.Count);
    }

    // §4.6 (owner, 2026-10-12): the sheet shows the item's body and link as GET /items/{itemId} serves them.
    [Fact]
    public async Task The_sheet_shows_the_items_body_and_link()
    {
        var sheet = await Sheet();

        await sheet.Open(_api, A.NewItem, Token);
        Assert.Equal((true, "Check the queue shows a new item without a click.", true, "linear:issue/ENG-142"), (sheet.HasBody, sheet.Body, sheet.HasLink, sheet.Link));

        await sheet.Open(_api, A.DecisionItem, Token);
        Assert.Equal((false, false), (sheet.HasBody, sheet.HasLink));
    }

    // README X61 (multiplayer later): createdBy and each entry's actor are opaque strings, shown exactly as served.
    [Fact]
    public async Task Actors_are_shown_exactly_as_served()
    {
        _server.Items[A.NewItem] = new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("items-id", "new").Replace("\"human:cli\"", "\"human:ada\"", StringComparison.Ordinal));
        _server.TimelinePages[0] = new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("items-id-timeline", "fixture").Replace("\"human:cli\"", "\"human:ada@laptop\"", StringComparison.Ordinal));
        var sheet = await Sheet();

        await sheet.Open(_api, A.NewItem, Token);

        Assert.Equal(("human:ada", "human:ada@laptop"), (sheet.CreatedBy, sheet.Timeline[0].Actor));
    }

    // §4.6: the timeline is paged by 'next'; Load more appends the next page and goes once there is none.
    [Fact]
    public async Task Load_more_appends_the_next_page_until_there_is_none()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);

        await sheet.LoadMore(Token);

        Assert.Equal((101, 152L), (sheet.Timeline.Count, sheet.Timeline[^1].Sequence));
        Assert.False(sheet.HasMore);
    }

    // §7, §8: the sheet's actions are the item's 'actions' that the catalog has, in the API's order, named with the item.
    [Fact]
    public async Task The_actions_are_the_items_actions_rendered_from_the_catalog()
    {
        var sheet = await Sheet();

        await sheet.Open(_api, A.NewItem, Token);

        Assert.Equal(["Close item", "Close as cancelled"], sheet.Actions.Select(a => a.Label), StringComparer.Ordinal);
        Assert.All(sheet.Actions, a => Assert.True(a.CanRun));
        Assert.Equal((false, true), (sheet.Actions[0].IsDestructive, sheet.Actions[1].IsDestructive));
        Assert.Equal("Close item " + A.NewItem, sheet.Actions[0].Name);
    }

    // §4.5: Close item sends the version the human saw; the sheet then follows the views (no folding of the answer).
    [Fact]
    public async Task Close_done_sends_the_version_shown_and_a_refresh_shows_the_new_state()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var close = sheet.Actions[0];

        await close.Press(Token);
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        await sheet.Refresh(_api, Token);

        Assert.Equal([(A.NewItem, new Ui.Api.CloseItemRequest("done", 1))], _server.Closed);
        Assert.Equal(("Done", "version 2", 1), (sheet.StateWord, sheet.VersionText, sheet.Actions.Count));   // L8: the control shows its outcome first
        _clock.Advance(ControlViewModel.AcceptedFor);
        Assert.Empty(sheet.Actions);
    }

    // §9.1, §18 U2's signal: a stale version is SND-ITEM-409; the sheet shows its detail as served and re-reads the item.
    [Fact]
    public async Task A_stale_close_shows_SND_ITEM_409s_detail_and_rereads_the_item()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _server.Close = RouteAnswer.Problem("items-id-close", "stale-version");
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");   // closed elsewhere since
        var cancel = sheet.Actions[1];

        await cancel.Press(Token);
        Assert.True(cancel.IsConfirming);
        await cancel.Confirm(Token);

        Assert.Equal([(A.NewItem, new Ui.Api.CloseItemRequest("cancelled", 1))], _server.Closed);
        Assert.Equal(("SND-ITEM-409", A.Detail("items-id-close", "stale-version"), true), (sheet.ProblemCode, sheet.ProblemDetail, sheet.HasProblem));
        Assert.Equal(("Done", "version 2"), (sheet.StateWord, sheet.VersionText));
        Assert.Equal(2, _server.ItemCalls);
    }

    // §4.9 Item detail: an unknown item is SND-ITEM-404's detail with Back.
    [Fact]
    public async Task An_unknown_item_shows_SND_ITEM_404s_detail()
    {
        var sheet = await Sheet();

        await sheet.Open(_api, A.MissingItem, Token);

        Assert.Equal((true, "SND-ITEM-404", A.Detail("items-id", "not-found")), (sheet.IsNotFound, sheet.ProblemCode, sheet.ProblemDetail));
        Assert.Empty(sheet.Actions);
    }

    // §4.1: a refresh keeps each control (and its lifecycle) while the item still allows its action.
    [Fact]
    public async Task A_refresh_keeps_the_same_controls()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var before = sheet.Actions.ToList();

        await sheet.Refresh(_api, Token);

        Assert.Equal(before, sheet.Actions);
        Assert.Equal(2, _server.TimelineCalls);
    }

    // The needs-decision item says what it waits on.
    [Fact]
    public async Task An_item_waiting_on_a_decision_names_it()
    {
        var sheet = await Sheet();

        await sheet.Open(_api, A.DecisionItem, Token);

        Assert.Equal((true, "DEC-0192a1b2c3d4e5f6a7b8c9d0e1f2c501", "Needs decision"), (sheet.HasWaitingOn, sheet.WaitingOn, sheet.StateWord));
        Assert.Equal(["Decide", "Close item", "Close as cancelled"], sheet.Actions.Select(a => a.Label), StringComparer.Ordinal);
        Assert.True(sheet.Actions[0].IsPending);
    }

    // L6: an answer for an item the sheet has moved on from is dropped, whatever order the reads finish in.
    [Fact]
    public async Task A_held_refresh_of_one_item_never_overwrites_the_item_opened_since()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var gate = _server.ItemGates[A.NewItem] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = sheet.Refresh(_api, Token);

        await sheet.Open(_api, A.DecisionItem, Token);
        gate.SetResult();
        await held;

        Assert.Equal((A.DecisionItem, "Needs decision", 3), (sheet.ItemId, sheet.StateWord, sheet.Actions.Count));
    }

    [Fact]
    public async Task A_held_first_read_of_one_item_is_dropped_when_another_is_opened()
    {
        var sheet = await Sheet();
        var gate = _server.ItemGates[A.NewItem] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = sheet.Open(_api, A.NewItem, Token);

        await sheet.Open(_api, A.DecisionItem, Token);
        gate.SetResult();
        await held;

        Assert.Equal((A.DecisionItem, "Needs decision", false), (sheet.ItemId, sheet.StateWord, sheet.IsLoading));
    }

    // L6: Load more while a page is in flight does nothing (the button stays focusable, so a second press is possible).
    [Fact]
    public async Task A_second_load_more_while_one_is_in_flight_reads_nothing()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var gate = _server.TimelineGates[151] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = _server.TimelineCalls;
        var first = sheet.LoadMore(Token);

        var second = sheet.LoadMore(Token);
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(calls + 1, _server.TimelineCalls);
        Assert.Equal(101, sheet.Timeline.Count);
    }

    // L7 (corr-7): a first read that fails with anything but 404 shows the API's code and detail; the next good read clears them.
    [Fact]
    public async Task A_failed_first_read_shows_the_apis_code_and_detail_until_a_read_succeeds()
    {
        _server.Items[A.NewItem] = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views didn't catch up within 2 s (fix: retry)");
        var sheet = await Sheet();

        await sheet.Open(_api, A.NewItem, Token);

        Assert.Equal((false, false, true, "SND-API-011", "The views didn't catch up within 2 s (fix: retry)"), (sheet.IsLoading, sheet.HasItem, sheet.HasProblem, sheet.ProblemCode, sheet.ProblemDetail));

        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "new");
        await sheet.Refresh(_api, Token);

        Assert.Equal((true, false), (sheet.HasItem, sheet.HasProblem));
    }

    // L13 (testing-1): a read made while the views are behind the last command is SND-API-011, once. The sheet keeps its last
    // answer marked stale; the next read (the views caught up) shows the new state.
    [Fact]
    public async Task A_read_the_views_are_behind_for_keeps_the_last_answer_stale_until_they_catch_up()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var before = (sheet.StateWord, sheet.VersionText, sheet.Timeline.Count);
        _server.Lag = ViewsLag.Behind;
        _ = await _server.CloseItem(_api, A.NewItem, new Ui.Api.CloseItemRequest("done", 1), Token);
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");

        await sheet.Refresh(_api, Token);

        Assert.Equal((true, true, before), (sheet.IsStale, sheet.HasItem, (sheet.StateWord, sheet.VersionText, sheet.Timeline.Count)));

        await sheet.Refresh(_api, Token);

        Assert.Equal((false, "Done", "version 2"), (sheet.IsStale, sheet.StateWord, sheet.VersionText));
    }
}
