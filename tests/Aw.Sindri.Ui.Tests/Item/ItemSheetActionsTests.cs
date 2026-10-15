using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>
/// The sheet's controls through a Close (UI spec §4.1, §9.1): a control in flight stays until it settles, the sheet speaks
/// the problem a gone control leaves behind once, SND-ITEM-409 where the control survives, and a question that outlives
/// the version it was asked about.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ItemSheetActionsTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.SpecShaped("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer NewAtVersion(int version) =>
        new(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("items-id", "new").Replace("\"version\": 1,", "\"version\": " + version.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",", StringComparison.Ordinal));

    private async Task<ItemSheetViewModel> Sheet()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        return new ItemSheetViewModel(_server, catalog, _clock);
    }

    private static List<string> Spoken(ItemSheetViewModel sheet)
    {
        var spoken = new List<string>();
        sheet.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ItemSheetViewModel.Announcement) && sheet.IsAnnouncing)
            {
                spoken.Add(sheet.Announcement);
            }
        };
        return spoken;
    }

    private static List<string> Spoken(ControlViewModel control)
    {
        var spoken = new List<string>();
        control.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ControlViewModel.Announcement) && control.IsAnnouncing)
            {
                spoken.Add(control.Announcement);
            }
        };
        return spoken;
    }

    // L8: the item moved on before the answer came back; the control that sent the command stays to show its outcome.
    [Fact]
    public async Task A_control_in_flight_stays_until_it_settles_even_when_the_item_no_longer_allows_it()
    {
        _server.OnClosed = (_, _) => _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var close = sheet.Actions[0];
        var spoken = Spoken(close);

        await close.Press(Token);
        await sheet.Refresh(_api, Token);

        Assert.Equal((CommandPhase.Accepted, "Done", true), (close.Phase, sheet.StateWord, sheet.Actions.Contains(close)));
        _clock.Advance(ControlViewModel.AcceptedFor);

        Assert.Equal((CommandPhase.Idle, 0), (close.Phase, sheet.Actions.Count));
        _ = Assert.Single(spoken, s => s.EndsWith("done", StringComparison.Ordinal));
    }

    // L8: the control that sent the refused command is gone with its action; the sheet says why, once, and the control stays quiet.
    [Fact]
    public async Task A_409_for_a_control_the_item_no_longer_allows_is_spoken_once_by_the_sheet()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _server.Close = RouteAnswer.Problem("items-id-close", "stale-version");
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        var cancel = sheet.Actions[1];
        var sheetSpoken = Spoken(sheet);
        var controlSpoken = Spoken(cancel);
        Assert.Equal((false, "Sindri"), (sheet.IsAnnouncing, sheet.Announcement));

        await cancel.Press(Token);
        await cancel.Confirm(Token);
        await sheet.Refresh(_api, Token);

        Assert.Equal(["SND-ITEM-409: " + A.Detail("items-id-close", "stale-version")], sheetSpoken);
        Assert.DoesNotContain(controlSpoken, s => s.StartsWith("SND-ITEM-409", StringComparison.Ordinal));
        Assert.Empty(sheet.Actions);
    }

    // L8: the 404 line is spoken once too, however many reads find the item still missing.
    [Fact]
    public async Task A_404_is_spoken_once_by_the_sheet()
    {
        var sheet = await Sheet();
        var spoken = Spoken(sheet);

        await sheet.Open(_api, A.MissingItem, Token);
        await sheet.Refresh(_api, Token);

        Assert.Equal(["SND-ITEM-404: " + A.Detail("items-id", "not-found")], spoken);
    }

    // L9 (testing-3): the control survives the re-read, so the refusal shows on it and not on the sheet; the retry sends the new version.
    [Fact]
    public async Task A_409_where_the_control_survives_shows_inline_and_the_retry_sends_the_new_version()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _server.Close = RouteAnswer.Problem("items-id-close", "stale-version");
        _server.Items[A.NewItem] = NewAtVersion(2);
        var close = sheet.Actions[0];

        await close.Press(Token);

        Assert.Equal((false, "SND-ITEM-409", A.Detail("items-id-close", "stale-version")), (sheet.HasProblem, close.ErrorCode, close.ErrorDetail));
        Assert.Equal("version 2", sheet.VersionText);
        Assert.Contains(close, sheet.Actions);

        _server.Close = RouteAnswer.SpecShaped("items-id-close", "accepted");
        await close.Press(Token);

        Assert.Equal([(A.NewItem, new Ui.Api.CloseItemRequest("done", 1)), (A.NewItem, new Ui.Api.CloseItemRequest("done", 2))], _server.Closed);
    }

    // corr-9: the human answers the question they were asked; when the item moved to another version meanwhile, the question goes.
    [Fact]
    public async Task A_question_asked_at_one_version_is_cancelled_when_the_item_moves_to_another()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var cancel = sheet.Actions[1];
        await cancel.Press(Token);
        Assert.True(cancel.IsConfirming);

        _server.Items[A.NewItem] = NewAtVersion(2);
        await sheet.Refresh(_api, Token);

        Assert.False(cancel.IsConfirming);
        Assert.Empty(_server.Closed);
    }

    // corr-9: a refresh that finds the same version leaves the question alone.
    [Fact]
    public async Task A_question_stays_while_the_version_does()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var cancel = sheet.Actions[1];
        await cancel.Press(Token);

        await sheet.Refresh(_api, Token);

        Assert.True(cancel.IsConfirming);
    }
}
