using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
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
        Assert.Equal(12, queue.Total);
    }

    // A held refresh that only removes rows has nothing new: no pill, and it shows on leave.
    [Fact]
    public async Task A_held_refresh_without_new_rows_shows_no_pill()
    {
        var queue = await Queue();
        queue.Hold(holding: true);
        _server.QueueView = RouteAnswer.SpecShaped("queue", "empty");
        await queue.Refresh(_api, Token);

        Assert.Equal((5, false), (queue.Total, queue.HasHeld));
        queue.Hold(holding: false);
        Assert.Equal(0, queue.Total);
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
}
