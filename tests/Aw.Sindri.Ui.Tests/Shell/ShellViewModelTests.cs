using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The shell's contract (UI spec §3.2, §3.3, §4.1; U0 plan Unit C item 6): the shell over the in-memory endpoint record,
/// API, clock and preferences, restored from the preferences the way the app starts it.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ShellViewModelTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The shell as the app starts it: built over the ports, then restored from the stored preferences.
    private async Task<ShellViewModel> Shell()
    {
        var shell = new ShellViewModel(_records, _server, _clock, _preferences);
        await shell.Restore(Token);
        return shell;
    }

    // Runs the shell until it reaches the state.
    private async Task<ShellViewModel> Running(ShellState until)
    {
        var shell = await Shell();
        _ = shell.Run(Token);
        await Until(() => shell.State == until);

        Assert.Equal(until, shell.State);
        return shell;
    }

    // Stream events reach the shell on the thread pool: wait for them with a bound, not a count of yields.
    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }
    }

    [Fact]
    public async Task The_top_bar_is_Home_Queue_Decisions_Services_with_no_counts_yet()
    {
        var shell = await Shell();

        Assert.Equal([PageId.Home, PageId.Queue, PageId.Decisions, PageId.Services], shell.Navigation.Select(n => n.Page));
        Assert.Equal([Labels.Home, Labels.Queue, Labels.Decisions, Labels.Services], shell.Navigation.Select(n => n.Label), StringComparer.Ordinal);
        Assert.Equal(["H", "Q", "D", "S"], shell.Navigation.Select(n => n.Key), StringComparer.Ordinal);
        Assert.All(shell.Navigation, n => Assert.Equal(string.Empty, n.Count));
    }

    [Fact]
    public async Task Later_lists_the_views_the_api_doesnt_serve_yet()
    {
        var shell = await Shell();

        Assert.Equal([Labels.Agents, Labels.Repos, Labels.Today, Labels.AutomaticWork], shell.Later.Select(l => l.Label), StringComparer.Ordinal);
        Assert.All(shell.Later, l => Assert.Equal(Labels.NotServedYet, l.Note));
    }

    [Fact]
    public async Task Every_page_is_reachable_and_Home_is_the_default()
    {
        var shell = await Shell();

        Assert.Equal(Enum.GetValues<PageId>(), shell.Pages);
        Assert.Equal(PageId.Home, shell.CurrentPage);
    }

    [Fact]
    public async Task Going_to_a_page_shows_it_and_says_so()
    {
        var shell = await Shell();
        var changed = new List<string?>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        shell.GoTo(PageId.Health);

        Assert.Equal(PageId.Health, shell.CurrentPage);
        Assert.Contains(nameof(ShellViewModel.CurrentPage), changed, StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_item_link_opens_the_sheet_and_Esc_leaves_its_edge_tab()
    {
        var shell = await Shell();
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));

        shell.Navigate(link);
        Assert.True(shell.IsSheetOpen);
        Assert.Equal("ITM-01JC8", shell.SheetSubject);

        shell.CloseSheet();
        Assert.False(shell.IsSheetOpen);
        Assert.True(shell.HasEdgeTab);
        Assert.Equal("ITM-01JC8", shell.SheetSubject);
    }

    [Fact]
    public async Task The_home_needs_you_link_goes_Home_and_leaves_the_sheet_alone()
    {
        var shell = await Shell();
        shell.GoTo(PageId.Queue);

        shell.Navigate(DeepLink.HomeNeedsYou);

        Assert.Equal(PageId.Home, shell.CurrentPage);
        Assert.False(shell.IsSheetOpen);
        Assert.False(shell.HasEdgeTab);
    }

    [Fact]
    public async Task The_attention_band_stays_hidden_until_needs_you_is_served() => Assert.False((await Shell()).IsAttentionBandVisible);

    [Fact]
    public async Task Before_the_first_answer_the_shell_is_connecting()
    {
        var shell = await Shell();

        Assert.Equal((ShellState.Connecting, Labels.Connecting), (shell.State, shell.HealthPill));
    }

    [Fact]
    public async Task No_endpoint_record_is_not_running_with_the_reason()
    {
        var shell = await Running(ShellState.NotRunning);

        Assert.Equal(Labels.NoRecord(_records.Location), shell.Reason);
        Assert.Equal(Labels.NotRunning, shell.HealthPill);
    }

    [Fact]
    public async Task A_status_without_an_api_version_is_normal()
    {
        _records.Write(A.Record());

        var shell = await Running(ShellState.Normal);

        Assert.StartsWith(Labels.Running, shell.HealthPill, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_status_of_another_api_version_is_a_version_mismatch()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { ApiVersion = 2 };

        var shell = await Running(ShellState.VersionMismatch);

        Assert.Equal(Labels.Versions(2, SindriApiClient.AppApiVersion), shell.Reason);
    }

    [Fact]
    public async Task An_answer_this_build_cant_read_is_unreadable_and_names_its_code()
    {
        _records.Write(A.Record());
        _server.UnreadableStatus = true;

        var shell = await Running(ShellState.Unreadable);

        Assert.Contains("SND-UI-003", shell.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Thirty_seconds_without_an_event_or_ping_is_stale_and_an_event_freshens_it()
    {
        _records.Write(A.Record());
        var shell = await Running(ShellState.Normal);

        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);

        Assert.Equal(ShellState.Stale, shell.State);
        Assert.Contains(Labels.Stale, shell.HealthPill, StringComparison.Ordinal);

        _server.Push(A.Event(1));
        await Until(() => shell.State == ShellState.Normal);

        Assert.Equal(ShellState.Normal, shell.State);
    }

    [Fact]
    public async Task The_last_page_is_remembered_and_reopened()
    {
        (await Shell()).GoTo(PageId.Decisions);

        Assert.Equal("Decisions", (await new Preferences(_preferences).Load(Token)).LastPage);
        Assert.Equal(PageId.Decisions, (await Shell()).CurrentPage);
    }
}
