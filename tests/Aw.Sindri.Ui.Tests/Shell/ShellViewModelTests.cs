using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The shell's contract (UI spec §3.2, §3.3, §4.1; U0 plan Unit C item 6), red until Unit A implements it. Unit A's first
/// step: give <see cref="ShellViewModel"/> its ports, build it in <see cref="Shell"/> from the fakes below, and remove
/// 'Explicit = true' from each test. They are explicit, not skipped, so the merge gate stays green while they wait
/// (xUnit1004 forbids Skip); run them with: dotnet test --project tests/Aw.Sindri.Ui.Tests --explicit only.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ShellViewModelTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Unit A: new ShellViewModel(_records, _server, _clock, _preferences) (or the ports it settles on).
    private static ShellViewModel Shell() => new();

    // Runs the shell until it reaches the state, advancing the fake clock between looks; Unit A may replace this.
    private static async Task<ShellViewModel> Running(ShellState until)
    {
        var shell = Shell();
        _ = shell.Run(Token);
        for (var i = 0; i < 100 && shell.State != until; i++)
        {
            await Task.Yield();
        }

        Assert.Equal(until, shell.State);
        return shell;
    }

    [Fact(Explicit = true)]
    public void The_top_bar_is_Home_Queue_Decisions_Services_with_no_counts_yet()
    {
        var shell = Shell();

        Assert.Equal([PageId.Home, PageId.Queue, PageId.Decisions, PageId.Services], shell.Navigation.Select(n => n.Page));
        Assert.Equal([Labels.Home, Labels.Queue, Labels.Decisions, Labels.Services], shell.Navigation.Select(n => n.Label), StringComparer.Ordinal);
        Assert.Equal(["H", "Q", "D", "S"], shell.Navigation.Select(n => n.Key), StringComparer.Ordinal);
        Assert.All(shell.Navigation, n => Assert.Equal(string.Empty, n.Count));
    }

    [Fact(Explicit = true)]
    public void Later_lists_the_views_the_api_doesnt_serve_yet()
    {
        var shell = Shell();

        Assert.Equal([Labels.Agents, Labels.Repos, Labels.Today, Labels.AutomaticWork], shell.Later.Select(l => l.Label), StringComparer.Ordinal);
        Assert.All(shell.Later, l => Assert.Equal(Labels.NotServedYet, l.Note));
    }

    [Fact(Explicit = true)]
    public void Every_page_is_reachable_and_Home_is_the_default()
    {
        var shell = Shell();

        Assert.Equal(Enum.GetValues<PageId>(), shell.Pages);
        Assert.Equal(PageId.Home, shell.CurrentPage);
    }

    [Fact(Explicit = true)]
    public void Going_to_a_page_shows_it_and_says_so()
    {
        var shell = Shell();
        var changed = new List<string?>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        shell.GoTo(PageId.Health);

        Assert.Equal(PageId.Health, shell.CurrentPage);
        Assert.Contains(nameof(ShellViewModel.CurrentPage), changed, StringComparer.Ordinal);
    }

    [Fact(Explicit = true)]
    public void An_item_link_opens_the_sheet_and_Esc_leaves_its_edge_tab()
    {
        var shell = Shell();
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));

        shell.Navigate(link);
        Assert.True(shell.IsSheetOpen);
        Assert.Equal("ITM-01JC8", shell.SheetSubject);

        shell.CloseSheet();
        Assert.False(shell.IsSheetOpen);
        Assert.True(shell.HasEdgeTab);
        Assert.Equal("ITM-01JC8", shell.SheetSubject);
    }

    [Fact(Explicit = true)]
    public void The_home_needs_you_link_goes_Home_and_leaves_the_sheet_alone()
    {
        var shell = Shell();
        shell.GoTo(PageId.Queue);

        shell.Navigate(DeepLink.HomeNeedsYou);

        Assert.Equal(PageId.Home, shell.CurrentPage);
        Assert.False(shell.IsSheetOpen);
        Assert.False(shell.HasEdgeTab);
    }

    [Fact(Explicit = true)]
    public void The_attention_band_stays_hidden_until_needs_you_is_served() => Assert.False(Shell().IsAttentionBandVisible);

    [Fact(Explicit = true)]
    public void Before_the_first_answer_the_shell_is_connecting() => Assert.Equal((ShellState.Connecting, Labels.Connecting), (Shell().State, Shell().HealthPill));

    [Fact(Explicit = true)]
    public async Task No_endpoint_record_is_not_running_with_the_reason()
    {
        var shell = await Running(ShellState.NotRunning);

        Assert.Equal(Labels.NoRecord(_records.Location), shell.Reason);
        Assert.Equal(Labels.NotRunning, shell.HealthPill);
    }

    [Fact(Explicit = true)]
    public async Task A_status_without_an_api_version_is_normal()
    {
        _records.Write(A.Record());

        var shell = await Running(ShellState.Normal);

        Assert.StartsWith(Labels.Running, shell.HealthPill, StringComparison.Ordinal);
    }

    [Fact(Explicit = true)]
    public async Task A_status_of_another_api_version_is_a_version_mismatch()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { ApiVersion = 2 };

        var shell = await Running(ShellState.VersionMismatch);

        Assert.Equal(Labels.Versions(2, SindriApiClient.AppApiVersion), shell.Reason);
    }

    [Fact(Explicit = true)]
    public async Task An_answer_this_build_cant_read_is_unreadable_and_names_its_code()
    {
        _records.Write(A.Record());
        _server.UnreadableStatus = true;

        var shell = await Running(ShellState.Unreadable);

        Assert.Contains("SND-UI-003", shell.Reason, StringComparison.Ordinal);
    }

    [Fact(Explicit = true)]
    public async Task Thirty_seconds_without_an_event_or_ping_is_stale_and_an_event_freshens_it()
    {
        _records.Write(A.Record());
        var shell = await Running(ShellState.Normal);

        _clock.Advance(ShellViewModel.StaleAfter);
        for (var i = 0; i < 100 && shell.State != ShellState.Stale; i++)
        {
            await Task.Yield();
        }

        Assert.Equal(ShellState.Stale, shell.State);
        Assert.Contains(Labels.Stale, shell.HealthPill, StringComparison.Ordinal);

        _server.Push(A.Event(1));
        for (var i = 0; i < 100 && shell.State != ShellState.Normal; i++)
        {
            await Task.Yield();
        }

        Assert.Equal(ShellState.Normal, shell.State);
    }

    [Fact(Explicit = true)]
    public async Task The_last_page_is_remembered_and_reopened()
    {
        Shell().GoTo(PageId.Decisions);

        Assert.Equal("Decisions", (await _preferences.Read(Token)).LastPage);
        Assert.Equal(PageId.Decisions, Shell().CurrentPage);
    }
}
