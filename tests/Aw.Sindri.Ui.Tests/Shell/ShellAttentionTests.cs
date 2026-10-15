using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The shell's side of the attention summary (U3' T5): title, summary and live region follow the list and the state.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellAttentionTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NeedsYouFeed Feed(params NeedsYouEntry[] rows) => new(rows, "") { Counts = new HomeCounts(Running: 2, WithYou: 0, Queued: 1) };

    private async Task<ShellViewModel> Shell(bool running)
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences));
        await shell.Restore(Token);
        if (running)
        {
            _records.Write(A.Record());
            _ = shell.Run(Token);
            for (var i = 0; i < 400 && shell.State != ShellState.Normal; i++)
            {
                await Task.Delay(5, Token);
            }

            Assert.Equal(ShellState.Normal, shell.State);
        }

        return shell;
    }

    [Fact]
    public async Task The_window_title_follows_the_needs_you_count()
    {
        var shell = await Shell(running: true);
        var changed = new List<string?>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.Equal("Sindri", shell.WindowTitle);

        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));

        Assert.Equal("(2) Needs you · Sindri", shell.WindowTitle);
        Assert.Contains(changed, n => string.Equals(n, nameof(ShellViewModel.WindowTitle), StringComparison.Ordinal));

        shell.ShowNeedsYou(Feed());

        Assert.Equal("Sindri", shell.WindowTitle);
    }

    [Fact]
    public async Task The_summary_is_the_trays_and_the_badges_source()
    {
        var shell = await Shell(running: true);

        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Profile()));

        Assert.Equal((2, TrayState.Attention), (shell.Attention.Badge, shell.Attention.Tray.State));
        Assert.Equal("2 need you · 2 running · 0 with you · 1 queued", shell.Attention.SummaryLine);
    }

    [Fact]
    public async Task Before_the_first_answer_the_tray_is_hollow_and_nothing_is_counted()
    {
        var shell = await Shell(running: false);

        shell.ShowNeedsYou(Feed(NeedsYouRows.Park()));

        Assert.Equal(ShellState.Connecting, shell.State);
        Assert.Equal((0, TrayState.Stale, "Sindri"), (shell.Attention.Badge, shell.Attention.Tray.State, shell.WindowTitle));
    }

    [Fact]
    public async Task The_live_region_says_N_now_need_you_once_for_each_rise()
    {
        var shell = await Shell(running: true);
        Assert.Equal(Labels.Sindri, shell.NeedsYouAnnouncement);

        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));
        Assert.Equal("2 now need you", shell.NeedsYouAnnouncement);

        shell.ShowNeedsYou(Feed(NeedsYouRows.Park()));
        Assert.Equal("2 now need you", shell.NeedsYouAnnouncement);   // a fall is the list's "<id> cleared"
    }
}
