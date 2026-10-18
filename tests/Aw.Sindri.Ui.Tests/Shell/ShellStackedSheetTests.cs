using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>An item sheet stacked from an instance frame (review run 3, r3-1): the control that had focus left the tree, so the shell says so and the view gives the sheet's heading focus.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellStackedSheetTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task A_child_row_opening_its_item_stacks_the_sheet_and_says_so() => UiThread.Run(async () =>
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        var detail = FixtureInstances.Detail("project-build");
        for (var i = 0; i < 400 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(5, Token);
        }

        _server.WorkflowAnswer = null;
        shell.OpenInstance(detail.Address, detail.Id);
        for (var i = 0; i < 400 && shell.Instance.Children is null; i++)
        {
            await Task.Delay(5, Token);
        }

        var stacked = 0;
        shell.SheetStacked += (_, _) => stacked++;
        var row = shell.Instance.Children!.Groups.Single().Streams.SelectMany(s => s.Rows).First(r => r.CanOpenItem);

        row.OpenItem();

        Assert.Equal(1, stacked);
        Assert.True(shell.IsSheetOpen);
    }, Token);
}
