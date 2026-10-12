using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>What each pre-API state shows (UI spec §4.1): the panels' visibility, the pill's words, and the sheet over the page.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellStateTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Running(ShellState until)
    {
        var shell = new ShellViewModel(_records, _server, _clock, _preferences);
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Eventually(() => shell.State == until);
        return shell;
    }

    private static async Task Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition());
    }

    private static (bool Mismatch, bool Chrome, bool NotRunning, bool Connecting, bool Unreadable, bool Page) Shown(ShellViewModel s) =>
        (s.IsVersionMismatch, s.IsChromeVisible, s.IsNotRunning, s.IsConnecting, s.IsUnreadable, s.IsPageVisible);

    [Fact]
    public async Task Connecting_shows_the_chrome_and_the_skeleton_only()
    {
        var shell = new ShellViewModel(_records, _server, _clock, _preferences);
        await shell.Restore(Token);

        Assert.Equal((false, true, false, true, false, false), Shown(shell));
    }

    [Fact]
    public async Task Not_running_shows_the_chrome_and_its_panel_in_place_of_the_page()
    {
        var shell = await Running(ShellState.NotRunning);

        Assert.Equal((false, true, true, false, false, false), Shown(shell));
    }

    [Fact]
    public async Task A_version_mismatch_is_a_full_window_panel_with_no_chrome_or_data()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { ApiVersion = 2 };

        var shell = await Running(ShellState.VersionMismatch);

        Assert.Equal((true, false, false, false, false, false), Shown(shell));
        Assert.Equal(Labels.VersionMismatch, shell.Pill.Text);
    }

    [Fact]
    public async Task The_status_this_app_speaks_is_normal()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { ApiVersion = 1 };

        var shell = await Running(ShellState.Normal);

        Assert.Equal((false, true, false, false, false, true), Shown(shell));
        Assert.Equal("Running · updated 0s ago", shell.Pill.Text);
    }

    [Fact]
    public async Task The_pill_says_how_long_ago_the_views_were_read()
    {
        _records.Write(A.Record());
        var shell = await Running(ShellState.Normal);

        _clock.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal("Running · updated 5s ago", shell.Pill.Text);
        Assert.Equal(ShellState.Normal, shell.State);
    }

    [Fact]
    public async Task Unreadable_keeps_the_last_data_marked_stale_and_recovers_on_a_good_answer()
    {
        _records.Write(A.Record());
        var shell = await Running(ShellState.Normal);

        _server.UnreadableStatus = true;
        _server.Push(A.Event(1));
        await Eventually(() => shell.State == ShellState.Unreadable);

        Assert.Equal((false, true, false, false, true, true), Shown(shell));
        Assert.Equal(Labels.Unreadable + " · " + Labels.Stale, shell.Pill.Text);
        Assert.Equal("pid 4242 · epoch 7", shell.Health.Lease);

        _server.UnreadableStatus = false;
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return shell.State == ShellState.Normal;
        });
    }

    [Fact]
    public async Task A_status_without_the_lease_is_not_running()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { Running = false };

        var shell = await Running(ShellState.NotRunning);

        Assert.Equal(Labels.NoLease, shell.Reason);
        Assert.Equal(Labels.NotRunning, shell.Pill.Text);
    }

    [Fact]
    public async Task The_sheet_shows_only_over_a_visible_page()
    {
        var shell = await Running(ShellState.NotRunning);
        Assert.True(DeepLink.TryParse("decision/DEC-7", out var link));

        shell.Navigate(link);

        Assert.True(shell.IsSheetOpen);
        Assert.False(shell.IsSheetShown);
    }

    [Fact]
    public async Task The_edge_tab_reopens_the_last_subject()
    {
        _records.Write(A.Record());
        var shell = await Running(ShellState.Normal);
        Assert.True(DeepLink.TryParse("job/JOB-9", out var link));
        shell.Navigate(link);
        shell.CloseSheet();
        Assert.True(shell.IsEdgeTabShown);

        shell.ReopenSheet();

        Assert.True(shell.IsSheetShown);
        Assert.False(shell.IsEdgeTabShown);
        Assert.Equal("JOB-9", shell.SheetSubject);
    }

    [Fact]
    public async Task The_top_bar_selection_follows_the_page_and_selecting_a_segment_goes_there()
    {
        var shell = new ShellViewModel(_records, _server, _clock, _preferences);
        await shell.Restore(Token);

        shell.SelectedNav = shell.Navigation[2];
        Assert.Equal((PageId.Decisions, true), (shell.CurrentPage, shell.IsDecisions));

        shell.GoTo(PageId.Health);
        Assert.Null(shell.SelectedNav);
        Assert.True(shell.IsHealth);
    }

    // §4.1 Stale and §11.3: a stale pill is the ⌛ glyph and the word stale, still saying how old the views are, with a
    // Refresh beside it.
    private async Task<ShellViewModel> Stale()
    {
        _records.Write(A.Record());
        var shell = await Running(ShellState.Normal);
        _server.FailPing = true;
        _clock.Advance(ShellViewModel.StaleAfter);
        await Eventually(() => shell.State == ShellState.Stale);
        return shell;
    }

    [Fact]
    public async Task Stale_is_the_hourglass_and_the_word_with_the_age_and_a_refresh()
    {
        var shell = await Stale();

        Assert.Equal(("⌛", Labels.Stale, Labels.UpdatedAgo(ShellViewModel.StaleAfter)), (shell.Pill.Glyph, shell.Pill.Word, shell.Pill.Age));
        Assert.Equal(Labels.Stale + " · " + Labels.UpdatedAgo(ShellViewModel.StaleAfter), shell.Pill.Text);
        Assert.True(shell.Pill.IsRefreshShown);
        Assert.True(shell.Pill.IsStale);
    }

    [Fact]
    public async Task Refresh_reads_the_status_again_and_the_views_are_fresh()
    {
        var shell = await Stale();
        var before = _server.StatusCalls;

        await shell.Health.Refresh(Token);

        Assert.Equal(before + 1, _server.StatusCalls);
        Assert.Equal(ShellState.Normal, shell.State);
        Assert.Equal(("✓", Labels.Running, Labels.UpdatedAgo(TimeSpan.Zero)), (shell.Pill.Glyph, shell.Pill.Word, shell.Pill.Age));
        Assert.False(shell.Pill.IsRefreshShown);
    }

    [Fact]
    public async Task After_three_failed_refreshes_the_pill_says_Sindri_is_not_responding()
    {
        var shell = await Stale();
        _server.FailStatus = true;

        await shell.Health.Refresh(Token);
        await shell.Health.Refresh(Token);
        Assert.Equal(Labels.Stale, shell.Pill.Word);
        await shell.Health.Refresh(Token);

        Assert.Equal(ShellState.Stale, shell.State);
        Assert.Equal(("⌛", Labels.NotResponding), (shell.Pill.Glyph, shell.Pill.Word));
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(Labels.NotResponding, shell.Pill.Word);
    }

    [Fact]
    public async Task Each_state_puts_its_glyph_before_its_word()
    {
        var connecting = new ShellViewModel(_records, _server, _clock, _preferences);
        Assert.Equal((string.Empty, Labels.Connecting, string.Empty), (connecting.Pill.Glyph, connecting.Pill.Word, connecting.Pill.Age));

        var down = await Running(ShellState.NotRunning);
        Assert.Equal(("✕", Labels.NotRunning, string.Empty), (down.Pill.Glyph, down.Pill.Word, down.Pill.Age));
        Assert.True(down.Pill.IsDown);

        _records.Write(A.Record());
        _server.UnreadableStatus = true;
        var unreadable = await Running(ShellState.Unreadable);
        Assert.Equal(("✕", Labels.Unreadable, Labels.Stale), (unreadable.Pill.Glyph, unreadable.Pill.Word, unreadable.Pill.Age));
        Assert.True(unreadable.Pill.IsDown);

        _server.UnreadableStatus = false;
        var healthy = await Running(ShellState.Normal);
        Assert.Equal(("✓", Labels.Running), (healthy.Pill.Glyph, healthy.Pill.Word));
        Assert.True(healthy.Pill.IsHealthy);
    }

    // I7 (states.html Not running): the navigation is dimmed and disabled while nothing runs; it comes back when it does.
    [Fact]
    public async Task The_navigation_is_disabled_only_while_not_running()
    {
        Assert.True(new ShellViewModel(_records, _server, _clock, _preferences).IsNavigationEnabled);
        var shell = await Running(ShellState.NotRunning);
        Assert.False(shell.IsNavigationEnabled);

        _records.Write(A.Record());
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return shell.State == ShellState.Normal;
        });
        Assert.True(shell.IsNavigationEnabled);
    }
}
