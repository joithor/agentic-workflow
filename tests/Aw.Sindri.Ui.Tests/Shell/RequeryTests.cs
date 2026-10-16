using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The re-read map (U3' plan "Re-query map"): until the API serves what an event invalidates (P5, E-15) every event
/// re-reads every view the app shows, and needs-you always, because the badge and the tray show with the window closed.
/// These pass under that rule and keep passing when the app reads `invalidates` instead.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RequeryTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.State == ShellState.Normal && shell.Queue.Total > 0 && _server.NeedsYouCalls > 0 && shell.Attention.Badge > 0);
        return shell;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition(), "the condition never held");
    }

    [Fact]
    public async Task The_first_read_puts_the_rows_in_the_title_the_badge_and_the_summary()
    {
        var shell = await Connected();

        Assert.Equal(("(3) Needs you · Sindri", 3), (shell.WindowTitle, shell.Attention.Badge));
        Assert.Contains("3 need you", shell.Attention.SummaryLine, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Home")]
    [InlineData("Settings")]
    public async Task Every_event_rereads_needs_you_whatever_page_is_visible(string page)
    {
        var shell = await Connected();
        shell.GoTo(Enum.Parse<PageId>(page));
        var before = _server.NeedsYouCalls;

        _server.Push(A.Event(60, "heartbeat.tick"));

        await Until(() => _server.NeedsYouCalls > before);
    }

    [Theory]
    [InlineData("request.filed")]
    [InlineData("decision.recorded")]
    [InlineData("item.state-changed")]
    public async Task Request_filed_decision_recorded_and_item_state_changed_reread_needs_you_decisions_queue_and_the_open_item(string type)
    {
        var shell = await Connected();
        shell.Navigate(new DeepLink(DeepLinkKind.Item, A.NewItem));
        await Until(() => shell.Item.HasItem);
        var (needsYou, decisions, items) = (_server.NeedsYouCalls, _server.DecisionQueries.Count, _server.ItemCalls);
        _server.NeedsYouView = RouteAnswer.SpecShaped("needs-you", "after-decide");
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");

        _server.Push(A.Event(61, type));

        await Until(() => _server.NeedsYouCalls > needsYou && _server.DecisionQueries.Count > decisions && _server.ItemCalls > items && shell.Queue.Total == 12 && shell.Attention.Badge == 2);
    }

    [Fact]
    public async Task Service_ran_rereads_needs_you_and_services()
    {
        var shell = await Connected();
        var (needsYou, services) = (_server.NeedsYouCalls, _server.ServicesCalls);

        _server.Push(A.Event(62, "service.ran"));

        await Until(() => _server.NeedsYouCalls > needsYou && _server.ServicesCalls > services);
        Assert.NotNull(shell);
    }

    [Fact]
    public async Task Profile_checked_rereads_needs_you()
    {
        _ = await Connected();
        var before = _server.NeedsYouCalls;

        _server.Push(A.Event(63, "profile.checked"));

        await Until(() => _server.NeedsYouCalls > before);
    }

    [Fact]
    public async Task A_failed_needs_you_read_keeps_the_rows_the_badge_shows()
    {
        var shell = await Connected();

        _server.NeedsYouView = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-LEDGER-002", "the ledger is busy");
        var before = _server.NeedsYouCalls;
        _server.Push(A.Event(64, "service.ran"));

        await Until(() => _server.NeedsYouCalls > before);
        Assert.Equal(3, shell.Attention.Badge);
    }
}
