using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The shell marks the Workflows rows stale while the views are stalled or rebuilding, and clears the mark when the banner goes (review run 1, V16). No window.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellStaleTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("stalled")]
    [InlineData("rebuilding")]
    public Task Rows_are_stale_while_the_banner_shows_and_fresh_when_it_clears(string scenario) => UiThread.Run(async () =>
    {
        _server.ServiceView = RouteAnswer.SpecShaped("services", scenario);
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.Workflows.Groups.Count > 0 && shell.Workflows.IsStale, Token);
        Assert.All(shell.Workflows.Groups.SelectMany(g => g.Rows), r => Assert.True(r.IsStale));

        _server.ServiceView = RouteAnswer.SpecShaped("services", "fixture");
        _server.Push(A.Event(70, "workflow.started"));
        await ShellWait.Until(shell, () => !shell.Workflows.IsStale && !shell.Health.Banner.IsShown, Token);

        Assert.All(shell.Workflows.Groups.SelectMany(g => g.Rows), r => Assert.False(r.IsStale));
    }, Token);
}
