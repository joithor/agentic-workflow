using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Start;

/// <summary>The Start form in the shell (U5'): its openers, the item it is opened for, and the frame it opens.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellStartTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Connected(PageId page)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Workflows.Groups.Count > 0 && shell.Health.Catalog.Controls.Count > 0, Token);
        shell.GoTo(page);
        return shell;
    }

    [Fact]
    public Task N_on_Workflows_opens_the_form_and_no_longer_names_a_later_phase() => UiThread.Run(async () =>
    {
        var shell = await Connected(PageId.Workflows);

        Assert.True(shell.Press("N"));

        Assert.Equal((true, string.Empty, false), (shell.Start.IsOpen, shell.Workflows.KeyNote, shell.IsSheetOpen));
        await ShellWait.Until(shell, () => shell.Start.Recipes.Count > 0, Token);
    }, Token);

    [Fact]
    public Task Opening_the_form_with_an_item_sheet_open_fills_that_item() => UiThread.Run(async () =>
    {
        var shell = await Connected(PageId.Work);
        shell.OpenItem(A.NewItem);

        shell.OpenStart();

        Assert.Equal((true, A.NewItem), (shell.Start.IsOpen, shell.Start.ItemId));
        await ShellWait.Until(shell, () => shell.Start.Recipes.Count > 0, Token);
    }, Token);

    [Fact]
    public Task Opening_the_form_with_no_item_open_leaves_the_item_empty() => UiThread.Run(async () =>
    {
        var shell = await Connected(PageId.Home);

        shell.OpenStart();

        Assert.Equal((true, string.Empty), (shell.Start.IsOpen, shell.Start.ItemId));
        await ShellWait.Until(shell, () => shell.Start.Recipes.Count > 0, Token);
    }, Token);

    // The accepted answer opens the frame by the instance's address, with its id as the read's hint, and the form closes.
    [Fact]
    public Task An_accepted_start_opens_the_instance_frame_and_closes_the_form() => UiThread.Run(async () =>
    {
        var shell = await Connected(PageId.Workflows);
        var request = StartGoldens.Request();
        var golden = GoldenFiles.Node("workflow-instances-start", "started");
        shell.OpenStart();
        await ShellWait.Until(shell, () => shell.Start.Recipes.Count > 0, Token);
        await shell.Start.Choose(shell.Start.Recipes[0], Token);
        shell.Start.ItemId = request.Item;
        shell.Start.Fields[0].Value = "the spec";

        await shell.Start.Submit(Token);

        Assert.Equal((false, true, (string)golden["instance"]!["address"]!), (shell.Start.IsOpen, shell.IsInstanceSheet, shell.Instance.Address)); // why: the golden names its instance
    }, Token);
}
