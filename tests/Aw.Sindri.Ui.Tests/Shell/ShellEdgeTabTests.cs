using System.Text.RegularExpressions;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The edge tab of a closed sheet (issue 1089): the item's or workflow's readable title, the kind's plain word while it loads, never a raw address.</summary>
[Lane(TestLane.Unit)]
public sealed partial class ShellEdgeTabTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [GeneratedRegex(@"^ITM-|^desk:WFI-|/[0-9]+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RawAddress { get; }

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Workflows.Groups.Count > 0, Token);
        return shell;
    }

    [Fact]
    public Task An_item_sheets_edge_tab_reads_the_word_Item_then_the_items_title() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        shell.Navigate(new DeepLink(DeepLinkKind.Item, A.NewItem));
        shell.CloseSheet();
        Assert.Equal(Labels.EdgeTabItem, shell.EdgeTabText);

        await ShellWait.Until(shell, () => shell.Item.Title == "Try the queue", Token);
        Assert.Equal("Try the queue", shell.EdgeTabText);
        Assert.DoesNotMatch(RawAddress, shell.EdgeTabText);
    }, Token);

    [Fact]
    public Task A_workflow_sheets_edge_tab_reads_the_kind_word_before_its_title_loads_and_the_title_after() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var detail = FixtureInstances.Detail("project-build");

        shell.OpenInstance(detail.Address, detail.Id);
        shell.CloseSheet();
        Assert.Equal(Labels.EdgeTabWorkflowRun, shell.EdgeTabText);
        Assert.DoesNotMatch(RawAddress, shell.EdgeTabText);

        await ShellWait.Until(shell, () => shell.Instance.Phase == FramePhase.Shown && shell.Instance.Address == detail.Address, Token);
        Assert.Equal(detail.Title, shell.EdgeTabText);
        Assert.DoesNotMatch(RawAddress, shell.EdgeTabText);
    }, Token);

    [Theory]
    [InlineData("job/JOB-9")]
    [InlineData("needs-you/ITM-01JC8")]
    public Task Other_sheets_never_show_a_raw_address(string link) => UiThread.Run(async () =>
    {
        var shell = await Connected();
        Assert.True(DeepLink.TryParse(link, out var parsed));

        shell.Navigate(parsed);
        shell.CloseSheet();

        Assert.NotEmpty(shell.EdgeTabText);
        Assert.DoesNotMatch(RawAddress, shell.EdgeTabText);
    }, Token);
}
