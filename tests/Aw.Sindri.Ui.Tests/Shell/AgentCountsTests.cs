using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The Agents tab's count and the tray line follow the status' session counts (collector spec CU-1, CA-8; U11a T5).</summary>
[Lane(TestLane.Unit)]
public sealed class AgentCountsTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Running()
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _records.Write(A.Record());
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);
        return shell;
    }

    private static string TabCount(ShellViewModel shell) => shell.Navigation.First(n => n.Page == PageId.Agents).Count;

    [Fact]
    public Task Tab_count_is_empty_before_the_first_status() => UiThread.Run(() =>
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());

        Assert.Equal("", TabCount(shell));
        Assert.Equal("", shell.Agents.NavCount);
        return Task.CompletedTask;
    }, Token);

    [Fact]
    public Task Tab_count_is_status_sessions_live() => UiThread.Run(async () =>
    {
        _server.Current = A.Status() with { Sessions = new SessionCountsView(Live: 3, Asking: 1, Problems: 0) };
        var shell = await Running();
        await ShellWait.Until(shell, () => TabCount(shell).Length > 0, Token);

        Assert.Equal("3", TabCount(shell));
        Assert.Equal("3", shell.Agents.NavCount);
    }, Token);

    [Fact]
    public Task Tab_count_is_empty_when_the_status_serves_no_sessions() => UiThread.Run(async () =>
    {
        var shell = await Running();

        Assert.Equal("", TabCount(shell));
    }, Token);
}
