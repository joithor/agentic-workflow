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
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.SpecShaped("commands", "catalog-p3") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

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
        Assert.Equal(["item.created", "item.linked"], sheet.Timeline.Select(e => e.Type), StringComparer.Ordinal);
        Assert.Equal(("11:55:00", "human:cli", "Created “Try the queue” in agentic-workflow"), (sheet.Timeline[0].Time, sheet.Timeline[0].Actor, sheet.Timeline[0].Line));
        Assert.True(sheet.HasMore);
    }

    // §4.6: the timeline is paged by 'next'; Load more appends the next page and goes once there is none.
    [Fact]
    public async Task Load_more_appends_the_next_page_until_there_is_none()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);

        await sheet.LoadMore(Token);

        Assert.Equal([52L, 53L, 57L], sheet.Timeline.Select(e => e.Sequence));
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

        Assert.Equal([(A.NewItem, new Aw.Sindri.Ui.Api.CloseItemRequest("done", 1))], _server.Closed);
        Assert.Equal(("Done", "version 2", 0), (sheet.StateWord, sheet.VersionText, sheet.Actions.Count));
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

        Assert.Equal([(A.NewItem, new Aw.Sindri.Ui.Api.CloseItemRequest("cancelled", 1))], _server.Closed);
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
}
