using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>"Open all in Work" on a children group (review run 1, V17; screens §4.2): the frame closes and the Work page shows every item, with no filter left over. No window.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellOpenAllInWorkTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task It_closes_the_sheet_and_shows_the_Work_page_unfiltered() => UiThread.Run(async () =>
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        var detail = FixtureInstances.Detail("project-many-children");
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);
        shell.OpenInstance(detail.Address, detail.Id);
        await ShellWait.Until(shell, () => shell.Instance.Children is not null && shell.Instance.Address == detail.Address, Token);
        shell.Queue.Filter(QueueFilter.Running);
        shell.Queue.OpenFilter();
        shell.Queue.FilterText = "left over from before";

        shell.Instance.Children!.Groups[0].OpenAllInWork();

        Assert.Equal((false, PageId.Work), (shell.IsSheetOpen, shell.CurrentPage));
        Assert.Equal((QueueFilter.All, string.Empty, false), (shell.Queue.Showing, shell.Queue.FilterText, shell.Queue.IsFiltering));
    }, Token);
}
