using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Queue;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>
/// The queue (UI spec §4.5) and Home's Running and Queued panels (§3.2, §4.2) over GET /sindri/queue: groups, order,
/// empty sentences and actions all come from the API (§2 "no logic"); the app maps fields to words and visibility.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class QueueViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    // P3 serves no waitingOnYou rows; the P4 extra adds the needs-decision row (a pending Decide) these behaviours need.
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.SpecShaped("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static void IsObservable(object list) => _ = Assert.IsType<System.Collections.Specialized.INotifyCollectionChanged>(list, exactMatch: false);

    private async Task<QueueViewModel> Queue()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        return new QueueViewModel(_server, catalog, _clock);
    }

    [Fact]
    public async Task Groups_follow_the_apis_order_and_hide_the_empty_ones()
    {
        var queue = await Queue();

        await queue.Refresh(_api, Token);

        Assert.Equal(["Waiting on you", "With you", "Running", "Queued"], queue.Groups.Select(g => g.Label), StringComparer.Ordinal);
        Assert.Equal([1, 0, 0, 4], queue.Groups.Select(g => g.Count));
        Assert.Equal([true, false, false, true], queue.Groups.Select(g => g.IsVisible));
        Assert.Equal(["Stream limits 429 fallback", "Retry storm in outbox dispatcher", "Context map review for tabs.layout", "Try the queue"], queue.Groups[3].Rows.Select(r => r.Title), StringComparer.Ordinal);
        Assert.Equal(5, queue.Total);
        Assert.False(queue.IsEmpty);
    }

    // P3's own queue: only queued items, every other group empty with its served sentence, no primary action yet.
    [Fact]
    public async Task P3s_queue_shows_only_the_queued_group()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "fixture");
        var queue = await Queue();

        await queue.Refresh(_api, Token);

        Assert.Equal([false, false, false, true], queue.Groups.Select(g => g.IsVisible));
        Assert.Equal((4, "Nothing is waiting on you."), (queue.Total, queue.Groups[0].Empty));
        Assert.All(queue.Groups[3].Rows, r => Assert.Null(r.PrimaryAction));
    }

    // §4.5: each row is id, title, repo, state as a word, step and since, named with its id for the screen reader.
    [Fact]
    public async Task A_row_shows_the_apis_fields_as_words()
    {
        var queue = await Queue();

        await queue.Refresh(_api, Token);

        var row = queue.Groups[3].Rows[3];
        Assert.Equal((A.NewItem, "Try the queue", "agentic-workflow", "New", "5m"), (row.Id, row.Title, row.Repo, row.StateWord, row.Age));
        Assert.Equal(A.NewItem + " Try the queue, New", row.Name);
        Assert.Equal("Needs decision", queue.Groups[0].Rows[0].StateWord);
    }

    // §4.5: the row's primary action is the catalog's control for the API's primaryAction (pending rows drawn disabled).
    [Fact]
    public async Task A_rows_primary_action_is_the_catalogs_control()
    {
        var queue = await Queue();

        await queue.Refresh(_api, Token);

        var decide = queue.Groups[0].Rows[0].PrimaryAction;
        Assert.NotNull(decide);
        Assert.Equal(("Decide", true), (decide.Label, decide.IsPending));
        Assert.Equal("Decide " + A.DecisionItem + ", arrives in P4", decide.Name);
        Assert.Null(queue.Groups[3].Rows[0].PrimaryAction);
    }

    // §4.5 (owner, 2026-10-12): four groups; a parked item sits in Waiting on you with a "parked" badge from its state.
    [Fact]
    public async Task A_parked_item_carries_a_parked_badge_in_waiting_on_you()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-parked");
        var queue = await Queue();

        await queue.Refresh(_api, Token);

        var waiting = queue.Groups[0].Rows;
        Assert.Equal((true, "parked", "Parked"), (waiting[0].HasBadge, waiting[0].Badge, waiting[0].StateWord));
        Assert.False(waiting[1].HasBadge);
        Assert.All(queue.Groups[3].Rows, r => Assert.False(r.HasBadge));
    }

    // §4.5 Empty: every group hidden, and the view's own empty sentence as served.
    [Fact]
    public async Task An_empty_queue_shows_the_views_sentence()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "empty");
        var queue = await Queue();

        await queue.Refresh(_api, Token);

        Assert.True(queue.IsEmpty);
        Assert.Equal("Nothing in the queue. Add one with `sindri item add`.", queue.EmptyText);
        Assert.All(queue.Groups, g => Assert.False(g.IsVisible));
    }

    // §4.2, E-18: Home's Running and With you panel (With you first) and Queued panel each show their group's sentence when empty.
    [Fact]
    public async Task Homes_panels_show_each_groups_own_empty_sentence()
    {
        var queue = await Queue();

        await queue.Refresh(_api, Token);

        Assert.Equal(("RUNNING 0 · WITH YOU 0", "Nothing running.", 0), (queue.RunningHeader, queue.RunningEmpty, queue.RunningRows.Count));
        Assert.Equal(("QUEUED 4", string.Empty, 4), (queue.QueuedHeader, queue.QueuedEmpty, queue.QueuedRows.Count));
        Assert.False(queue.HasMoreRunning);
    }

    // §3.2 caps: Running and With you shows at most 5 rows, With you first, then "Show all N in Queue"; Queued shows 5.
    [Fact]
    public async Task Homes_panels_cap_at_five_rows_with_you_first()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        var queue = await Queue();

        await queue.Refresh(_api, Token);

        Assert.Equal("RUNNING 6 · WITH YOU 1", queue.RunningHeader);
        Assert.Equal(["Taken over by hand", "Running item 1", "Running item 2", "Running item 3", "Running item 4"], queue.RunningRows.Select(r => r.Title), StringComparer.Ordinal);
        Assert.Equal((true, "Show all 7 in Queue"), (queue.HasMoreRunning, queue.ShowAllRunning));
        Assert.Equal(string.Empty, queue.RunningEmpty);

        _server.QueueView = RouteAnswer.SpecShaped("queue", "large");
        await queue.Refresh(_api, Token);
        Assert.Equal((5, true, "Show all 30 in Queue"), (queue.QueuedRows.Count, queue.HasMoreQueued, queue.ShowAllQueued));
    }

    // §3.3: G R and G W show the Queue filtered to one group; the group's own sentence when it's empty.
    [Fact]
    public async Task A_filter_shows_one_group_with_its_own_sentence()
    {
        var queue = await Queue();
        await queue.Refresh(_api, Token);

        queue.Filter(QueueFilter.Running);

        Assert.Equal([false, false, true, false], queue.Groups.Select(g => g.IsVisible));
        Assert.Equal(("Nothing running.", true), (queue.Groups[2].Empty, queue.Groups[2].ShowsEmpty));
        Assert.False(queue.IsEmpty);

        queue.Filter(QueueFilter.All);
        Assert.Equal([true, false, false, true], queue.Groups.Select(g => g.IsVisible));
    }

    // §3.3: selection is kept by id across refreshes; a cleared row moves it to the next row and says so.
    [Fact]
    public async Task Selection_is_kept_by_id_and_a_cleared_row_moves_it_to_the_next()
    {
        var queue = await Queue();
        await queue.Refresh(_api, Token);
        queue.Select("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a302");

        await queue.Refresh(_api, Token);
        Assert.Equal("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a302", queue.SelectedId);
        Assert.Equal((false, Labels.Sindri), (queue.IsAnnouncing, queue.Announcement));

        // The selected row cleared; another took its place in the group.
        _server.QueueView = new RouteAnswer(HttpStatusCode.OK, GoldenFiles.SpecShaped("queue", "fixture").Replace("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a302", "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a3ff", StringComparison.Ordinal));
        await queue.Refresh(_api, Token);

        Assert.Equal("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a3ff", queue.SelectedId);
        Assert.Equal("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a302 cleared", queue.Announcement);
        Assert.True(queue.IsAnnouncing);
    }

    // L3 (§3.3, §11.2): "<id> cleared" goes through a gated polite region, as U1's do: the flag changes before the text, the
    // text rests on a neutral label (never ""), it is spoken once, and the next refresh puts the region dark again.
    [Fact]
    public async Task A_cleared_row_is_announced_once_through_a_gated_region()
    {
        var queue = await Queue();
        await queue.Refresh(_api, Token);
        queue.Select("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a302");
        var states = new List<(bool Live, string Text)>();
        queue.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(QueueViewModel.IsAnnouncing), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(QueueViewModel.Announcement), StringComparison.Ordinal))
            {
                states.Add((queue.IsAnnouncing, queue.Announcement));
            }
        };

        _server.QueueView = new RouteAnswer(HttpStatusCode.OK, GoldenFiles.SpecShaped("queue", "fixture").Replace("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a302", "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a3ff", StringComparison.Ordinal));
        await queue.Refresh(_api, Token);
        var cleared = Labels.Cleared("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a302");

        Assert.DoesNotContain(states, s => s.Text.Length == 0);
        Assert.Equal(1, states.Count(s => s.Live && s.Text == cleared));
        Assert.DoesNotContain(states, s => !s.Live && s.Text == cleared);

        await queue.Refresh(_api, Token);
        Assert.Equal((false, Labels.Sindri), (queue.IsAnnouncing, queue.Announcement));
    }

    // §8: J and K move between the visible rows in the API's order.
    [Fact]
    public async Task Next_and_previous_move_through_the_visible_rows()
    {
        var queue = await Queue();
        await queue.Refresh(_api, Token);

        queue.Move(1);
        Assert.Equal(A.DecisionItem, queue.SelectedId);
        queue.Move(1);
        Assert.Equal("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a301", queue.SelectedId);
        queue.Move(-1);
        Assert.Equal(A.DecisionItem, queue.SelectedId);
        queue.Move(-1);
        Assert.Equal(A.DecisionItem, queue.SelectedId);
        Assert.True(queue.Groups[0].Rows[0].IsSelected);
    }

    // L1 (§3.3): a refresh reconciles; groups by name and rows by id are reused (updated in place), so a collapsed
    // group stays collapsed and a control that has focus keeps its row.
    [Fact]
    public async Task A_refresh_reuses_groups_and_rows_and_keeps_a_collapsed_group_collapsed()
    {
        var queue = await Queue();
        await queue.Refresh(_api, Token);
        var (groups, queued, row) = (queue.Groups, queue.Groups[3], queue.Groups[3].Rows[3]);
        queued.IsExpanded = false;

        _server.QueueView = new RouteAnswer(HttpStatusCode.OK, GoldenFiles.SpecShaped("queue", "extra-p4-waiting").Replace("Try the queue", "Renamed item", StringComparison.Ordinal));
        await queue.Refresh(_api, Token);

        Assert.Same(groups, queue.Groups);
        Assert.Same(queued, queue.Groups[3]);
        Assert.False(queued.IsExpanded);
        Assert.Same(row, queue.Groups[3].Rows[3]);
        Assert.Equal(("Renamed item", A.NewItem + " Renamed item, New"), (row.Title, row.Name));
    }

    // L1: the lists the views bind are patched in place, so the controls built for them survive.
    [Fact]
    public async Task The_lists_the_views_bind_are_patched_in_place()
    {
        var queue = await Queue();
        await queue.Refresh(_api, Token);

        IsObservable(queue.Groups);
        Assert.All(queue.Groups, g => IsObservable(g.Rows));
        IsObservable(queue.RunningRows);
        IsObservable(queue.QueuedRows);
        var (running, queued) = (queue.RunningRows, queue.QueuedRows);

        await queue.Refresh(_api, Token);
        Assert.Same(running, queue.RunningRows);
        Assert.Same(queued, queue.QueuedRows);
    }

    // §4.1: the skeleton ends when the rows are there, never before (a waiter on !IsLoading must find them).
    [Fact]
    public async Task Loading_ends_only_once_the_first_answer_is_shown()
    {
        var queue = await Queue();
        var seen = new List<(int Total, int Groups, string Tab)>();
        queue.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(QueueViewModel.IsLoading) && !queue.IsLoading)
            {
                seen.Add((queue.Total, queue.Groups.Count, queue.RunningHeader));
            }
        };

        await queue.Refresh(_api, Token);

        Assert.Equal([(5, 4, "RUNNING 0 · WITH YOU 0")], seen);
    }

    // §4.1: skeleton rows on the first load only; a failed read keeps the last rows, marked stale.
    [Fact]
    public async Task The_first_load_is_loading_and_a_failed_read_keeps_the_rows_marked_stale()
    {
        var queue = await Queue();
        Assert.True(queue.IsLoading);

        await queue.Refresh(_api, Token);
        Assert.False(queue.IsLoading);

        _server.QueueView = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views didn't catch up within 2 s (fix: retry)");
        await queue.Refresh(_api, Token);

        Assert.Equal((5, true, false), (queue.Total, queue.IsStale, queue.IsLoading));
    }

    // L7 (corr-7): a first read that fails ends the skeleton and says what the API said, so the page is never blank.
    [Fact]
    public async Task A_failed_first_read_ends_loading_and_shows_the_apis_code_and_detail()
    {
        _server.QueueView = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views didn't catch up within 2 s (fix: retry)");
        var queue = await Queue();

        await queue.Refresh(_api, Token);

        Assert.Equal((false, true, "SND-API-011", "The views didn't catch up within 2 s (fix: retry)", false), (queue.IsLoading, queue.HasProblem, queue.ProblemCode, queue.ProblemDetail, queue.IsStale));

        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting");
        await queue.Refresh(_api, Token);

        Assert.Equal((false, false, 5), (queue.HasProblem, queue.IsLoading, queue.Total));
    }
}
