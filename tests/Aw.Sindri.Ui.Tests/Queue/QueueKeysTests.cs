using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Queue;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>
/// §8's guards and filter on the queue: new rows are held while the pointer is over a panel (an "N new" pill), single
/// keys are ignored for 500 ms after a refresh moved the selection on its own, and `/` filters by id, title and repo.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class QueueKeysTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    // P3 serves no waitingOnYou rows; the P4 extra adds the needs-decision row (a pending Decide) these behaviours need.
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.SpecShaped("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<QueueViewModel> Queue()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var queue = new QueueViewModel(_server, catalog, _clock);
        await queue.Refresh(_api, Token);
        return queue;
    }

    // §8: while the pointer is over a panel, new rows are held, not shifted in under it; the pill says how many.
    [Fact]
    public async Task Rows_are_held_while_the_pointer_is_over_a_panel_and_shown_on_leave()
    {
        var queue = await Queue();
        queue.Hold(holding: true);

        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        await queue.Refresh(_api, Token);

        Assert.Equal((5, true, "7 new"), (queue.Total, queue.HasHeld, queue.HeldText));

        queue.Hold(holding: false);
        Assert.Equal((12, false), (queue.Total, queue.HasHeld));
    }

    // The pill shows the held rows at once, even while the pointer stays.
    [Fact]
    public async Task The_pill_shows_the_held_rows_while_the_pointer_stays()
    {
        var queue = await Queue();
        queue.Hold(holding: true);
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        await queue.Refresh(_api, Token);

        queue.ShowHeld();

        Assert.Equal((12, false), (queue.Total, queue.HasHeld));
        _server.QueueView = RouteAnswer.SpecShaped("queue", "fixture");
        await queue.Refresh(_api, Token);
        Assert.Equal((4, false), (queue.Total, queue.HasHeld));   // L4: removals are not held
    }

    // a11y-3: the pill hides when it is pressed, taking focus with it: focus goes to the first row it showed.
    [Fact]
    public async Task The_pill_asks_for_focus_on_the_first_row_it_shows()
    {
        var queue = await Queue();
        queue.Hold(holding: true);
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        await queue.Refresh(_api, Token);
        var asked = new List<FocusRequestedEventArgs>();
        var known = queue.Groups.SelectMany(g => g.Rows).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        queue.FocusRequested += (_, e) => asked.Add(e);

        queue.ShowHeld();

        var first = queue.Groups.Where(g => g.IsVisible).SelectMany(g => g.Rows).First(r => !known.Contains(r.Id));
        Assert.Equal([(first.Id, false)], asked.Select(a => (a.ItemId, a.OnlyIfLost)));
    }

    // Leaving the panel shows the held rows too, but nobody pressed the pill: focus stays where it is.
    [Fact]
    public async Task Leaving_the_panel_shows_the_held_rows_without_asking_for_focus()
    {
        var queue = await Queue();
        queue.Hold(holding: true);
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        await queue.Refresh(_api, Token);
        var asked = 0;
        queue.FocusRequested += (_, _) => asked++;

        queue.Hold(holding: false);

        Assert.Equal(0, asked);
    }

    // L4 (§8): hold only keeps rows from shifting in under the pointer. A removal is shown at once: no pill, nothing stale to click.
    [Fact]
    public async Task A_held_refresh_without_new_rows_shows_no_pill_and_applies_the_removals()
    {
        var queue = await Queue();
        queue.Hold(holding: true);
        _server.QueueView = RouteAnswer.SpecShaped("queue", "empty");
        await queue.Refresh(_api, Token);

        Assert.Equal((0, false), (queue.Total, queue.HasHeld));
        queue.Hold(holding: false);
        Assert.Equal(0, queue.Total);
    }

    // L4: an in-place update (a new title) and a removal show at once while only the new rows wait behind the pill, which is spoken.
    [Fact]
    public async Task A_held_refresh_applies_updates_and_removals_and_holds_only_the_new_rows()
    {
        var queue = await Queue();
        queue.Hold(holding: true);
        var row = queue.Groups[3].Rows[3];

        var added = GoldenFiles.SpecShaped("queue", "extra-p11-running").Replace("Try the queue", "Renamed item", StringComparison.Ordinal);
        _server.QueueView = new RouteAnswer(System.Net.HttpStatusCode.OK, added);
        await queue.Refresh(_api, Token);

        Assert.Equal(("Renamed item", 5, true, "7 new"), (row.Title, queue.Total, queue.HasHeld, queue.HeldText));
        Assert.Equal((true, "7 new"), (queue.IsAnnouncing, queue.Announcement));

        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting");
        await queue.Refresh(_api, Token);
        Assert.Equal((false, 5), (queue.HasHeld, queue.Total));
    }

    // §8 key guard: for 500 ms after a refresh moved the selection on its own, single keys are ignored.
    [Fact]
    public async Task A_selection_moved_by_a_refresh_guards_single_keys_for_500_ms()
    {
        var queue = await Queue();
        queue.Select(A.NewItem);
        Assert.False(queue.IsGuarded);

        _server.QueueView = new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("queue", "fixture").Replace(A.NewItem, "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a3ff", StringComparison.Ordinal));
        await queue.Refresh(_api, Token);

        Assert.True(queue.IsGuarded);
        _clock.Advance(TimeSpan.FromMilliseconds(499));
        Assert.True(queue.IsGuarded);
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.False(queue.IsGuarded);
    }

    // §8 `/`: filter the list by id, title or repo; nothing matching says so.
    [Fact]
    public async Task The_filter_keeps_rows_whose_id_title_or_repo_match()
    {
        var queue = await Queue();

        queue.FilterText = "RETRY";
        Assert.Equal(["Retry storm in outbox dispatcher"], queue.Groups.SelectMany(g => g.Rows).Select(r => r.Title), StringComparer.Ordinal);
        Assert.Equal([false, false, false, true], queue.Groups.Select(g => g.IsVisible));

        queue.FilterText = "sindri-docs";
        Assert.Equal("Context map review for tabs.layout", Assert.Single(queue.Groups.SelectMany(g => g.Rows)).Title);

        queue.FilterText = "nothing like this";
        Assert.Equal((true, "Nothing matches"), (queue.IsEmpty, queue.EmptyText));
        Assert.Equal(5, queue.Total);

        queue.ClearFilter();
        Assert.Equal((false, string.Empty, 5), (queue.IsFiltering, queue.FilterText, queue.Groups.Sum(g => g.Rows.Count)));
    }

    // §8 `Y`: the selected row's CLI, from its primary action's catalog row.
    [Fact]
    public async Task The_selected_rows_cli_is_its_primary_actions()
    {
        var queue = await Queue();

        Assert.Null(queue.SelectedCopy());
        queue.Select(A.DecisionItem);
        Assert.Equal("sindri decide <decisionId> <choice> [--note <note>]", queue.SelectedCopy()?.Cli);
        queue.Select(A.NewItem);
        Assert.Null(queue.SelectedCopy());
    }

    // L2 (§8): J / K walk the rows of the page they are pressed on: Home's panels as drawn, the Queue page's groups as drawn.
    [Fact]
    public async Task J_walks_homes_rows_as_drawn_and_the_pages_expanded_groups()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        var queue = await Queue();
        var home = queue.RunningRows.Concat(queue.QueuedRows).Select(r => r.Id).ToList();

        var walked = new List<string?>();
        for (var i = 0; i < home.Count; i++)
        {
            queue.Move(1, QueueScope.Home);
            walked.Add(queue.SelectedId);
        }

        Assert.Equal(home, walked.Select(w => w), StringComparer.Ordinal);

        queue.Select(string.Empty);
        queue.Groups.Single(g => g.Group == "queued").IsExpanded = false;
        var page = queue.Groups.Where(g => g.IsVisible && g.IsExpanded).SelectMany(g => g.Rows).Select(r => r.Id).ToList();
        var seen = new List<string?>();
        for (var i = 0; i < page.Count + 2; i++)
        {
            queue.Move(1, QueueScope.Page);
            seen.Add(queue.SelectedId);
        }

        Assert.Equal([.. page, page[^1], page[^1]], seen.Select(w => w), StringComparer.Ordinal);
    }

    // L2: a filter change moves the selection off a row it hides (silently: no announcement, no key guard).
    [Fact]
    public async Task A_filter_change_clears_a_selection_it_hides_silently()
    {
        var queue = await Queue();
        queue.Select(A.NewItem);

        queue.FilterText = "Try the";
        Assert.Equal(A.NewItem, queue.SelectedId);

        queue.FilterText = "RETRY";
        Assert.Equal((null, Labels.Sindri, false), (queue.SelectedId, queue.Announcement, queue.IsGuarded));

        queue.Select(A.NewItem);
        queue.ClearFilter();
        queue.Select(A.NewItem);
        queue.Filter(QueueFilter.Running);
        Assert.Null(queue.SelectedId);
    }

    // L2: a row the filter hides is not a row that is gone: a refresh keeps it selected and says nothing.
    [Fact]
    public async Task A_refresh_keeps_a_selection_the_filter_hides()
    {
        var queue = await Queue();
        queue.FilterText = "RETRY";
        queue.Select(A.NewItem);

        await queue.Refresh(_api, Token);

        Assert.Equal((A.NewItem, false, Labels.Sindri), (queue.SelectedId, queue.IsAnnouncing, queue.Announcement));
    }

    // a11y-11: the title button is named for what it does.
    [Fact]
    public async Task A_rows_title_button_is_named_open_and_its_id()
    {
        var queue = await Queue();

        Assert.Equal("Open " + A.NewItem, queue.Groups[3].Rows[3].OpenName);
    }

    // The golden queue with the item removed (the projector cleared it), or its primary action set.
    private static RouteAnswer Edited(string view, Action<System.Text.Json.Nodes.JsonArray> edit)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.SpecShaped("queue", view))!;
        foreach (var group in root["groups"]!.AsArray())
        {
            edit(group!["items"]!.AsArray());
        }

        return new RouteAnswer(System.Net.HttpStatusCode.OK, root.ToJsonString());
    }

    private static RouteAnswer Without(string view, string itemId) =>
        Edited(view, items => items.Where(i => i!["id"]!.GetValue<string>() == itemId).ToList().ForEach(i => items.Remove(i)));

    private static string Q(int n) => "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a30" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static List<string> FocusAsked(QueueViewModel queue)
    {
        var asked = new List<string>();
        queue.FocusRequested += (_, e) => asked.Add(e.ItemId);
        return asked;
    }

    // corr-3 (G R, Home): the Queued panel is Home's whatever the Queue page's filter shows; its cleared row passes to the next queued row.
    [Fact]
    public async Task A_queued_row_cleared_on_home_with_the_running_filter_on_selects_the_next_queued_row()
    {
        var queue = await Queue();
        queue.Filter(QueueFilter.Running);
        queue.Move(1, QueueScope.Home);
        queue.Move(1, QueueScope.Home);
        Assert.Equal(Q(2), queue.SelectedId);
        var asked = FocusAsked(queue);

        _server.QueueView = Without("extra-p4-waiting", Q(2));
        await queue.Refresh(_api, Token);

        Assert.Equal((Q(3), true), (queue.SelectedId, queue.QueuedRows.Single(r => r.Id == Q(3)).IsSelected));
        Assert.Equal([Q(3)], asked);
    }

    // corr-3 (G W left on, Home): the same, with the With you filter still on.
    [Fact]
    public async Task A_queued_row_cleared_on_home_with_the_with_you_filter_left_on_selects_the_next_queued_row()
    {
        var queue = await Queue();
        queue.Filter(QueueFilter.WithYou);
        queue.Move(1, QueueScope.Home);
        Assert.Equal(Q(1), queue.SelectedId);

        _server.QueueView = Without("extra-p4-waiting", Q(1));
        await queue.Refresh(_api, Token);

        Assert.Equal(Q(2), queue.SelectedId);
    }

    // corr-3 (FilterText set, Home): Home's panels ignore the Queue page's text filter; so does the highlight and Y.
    [Fact]
    public async Task A_row_the_queue_pages_text_filter_hides_is_still_selected_and_copyable_on_home()
    {
        _server.QueueView = Edited("extra-p4-waiting", items =>
        {
            foreach (var item in items.Where(i => i!["id"]!.GetValue<string>() == Q(1)))
            {
                item!["primaryAction"] = "Decide";
            }
        });
        var queue = await Queue();
        queue.FilterText = "no row has this";

        queue.Move(1, QueueScope.Home);

        Assert.Equal((Q(1), true), (queue.SelectedId, queue.QueuedRows[0].IsSelected));
        Assert.NotNull(queue.SelectedCopy());
    }
}
