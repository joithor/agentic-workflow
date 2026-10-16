using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The shell's state for the Workflows page and the instance frame's trail, with no window (review run 1, V10 of the tests): the tab's count, an item opened from a frame and Back, and Esc ending the whole trail.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellTrailTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Workflows.Groups.Count > 0, Token);
        return shell;
    }

    private static async Task Open(ShellViewModel shell, string scenario)
    {
        var detail = FixtureInstances.Detail(scenario);
        shell.OpenInstance(detail.Address, detail.Id);
        await ShellWait.Until(shell, () => shell.Instance.Phase == FramePhase.Shown && shell.Instance.Address == detail.Address, Token);
    }

    [Fact]
    public Task The_Workflows_nav_count_is_counts_open() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        var open = FixtureInstances.Counts.Open.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var entry = shell.Navigation.First(n => n.Page == PageId.Workflows);
        Assert.Equal((open, true), (entry.Count, entry.HasCount));
    }, Token);

    [Fact]
    public Task An_items_Open_opens_its_sheet_stacked_and_Back_returns_to_the_frame() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        await Open(shell, "project-build");
        var parent = shell.Instance.Address;
        var row = shell.Instance.Children!.Groups.Single().Streams[0].Rows[0];

        row.OpenItem();
        Assert.Equal((DeepLinkKind.Item, row.ItemId), (shell.SheetLink!.Kind, shell.SheetSubject));

        shell.Back();
        await ShellWait.Until(shell, () => shell.IsInstanceSheet && shell.Instance.Address == parent && shell.Instance.Phase == FramePhase.Shown, Token);
    }, Token);

    [Fact]
    public Task Esc_closes_the_whole_stack_and_leaves_no_trail() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        await Open(shell, "project-build");
        var project = FixtureInstances.Detail("project-build").Address;
        shell.Instance.Children!.Groups.Single().Streams[0].Rows[0].OpenFrame();
        await ShellWait.Until(shell, () => shell.Instance.Address != project, Token);

        shell.CloseSheet();
        Assert.False(shell.IsSheetOpen);
        shell.Navigate(new DeepLink(DeepLinkKind.Workflow, project));
        await ShellWait.Until(shell, () => shell.Instance.Phase == FramePhase.Shown, Token);
        shell.Back();

        Assert.False(shell.IsSheetOpen);
    }, Token);
}
