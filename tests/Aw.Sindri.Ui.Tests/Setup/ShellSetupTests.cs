using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The Setup card and page in the shell: read on connect and with every event, G U opens the page, the card's button opens the approve-all dialog, and Home hides a setup row for a step the card draws.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellSetupTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new()
    {
        Catalog = RouteAnswer.SpecShaped("commands", "extra-approve-all-ready"),
        NeedsYouView = RouteAnswer.Api("needs-you", "empty"),
        SetupAnswer = RouteAnswer.SpecShaped("setup", "extra-priority-mix"),
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Setup.Steps.Count > 0, Token);
        return shell;
    }

    [Fact]
    public Task Setup_is_read_on_connect_and_again_with_every_event() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var before = _server.SetupCalls;

        await shell.Health.RefreshViews(new Uri("http://127.0.0.1:7410/"), Token);

        Assert.True(before >= 1);
        Assert.True(_server.SetupCalls > before);
        Assert.Equal(["repo", "profile", "recipes", "mcp", "map", "onyx"], shell.Setup.Steps.Select(s => s.Id), StringComparer.Ordinal);
    }, Token);

    [Fact]
    public Task G_U_goes_to_the_Setup_page() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        _ = shell.PressKey("G");
        _ = shell.PressKey("U");

        Assert.Equal((PageId.Setup, true, "Setup"), (shell.CurrentPage, shell.IsSetup, shell.PageName));
    }, Token);

    [Fact]
    public Task Setup_is_a_page_but_not_a_tab() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        shell.GoTo(PageId.Setup);

        Assert.Contains(PageId.Setup, shell.Pages);
        Assert.DoesNotContain(shell.Navigation, n => n.Page == PageId.Setup);
        Assert.Null(shell.SelectedNav);
    }, Token);

    [Fact]
    public Task The_card_opens_the_page() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        shell.Setup.RequestOpenPage();

        Assert.Equal(PageId.Setup, shell.CurrentPage);
    }, Token);

    [Fact]
    public Task The_approve_all_button_on_the_card_opens_the_dialog_and_approves_nothing() => UiThread.Run(async () =>
    {
        var list = SetupFixture.With("profile-needs-approval", "recipes", "needs-you", "ApproveAllRecipes");
        _server.SetupAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
        var shell = await Connected();
        await ShellWait.Until(shell, () => shell.Health.Catalog.Controls.Count > 0, Token);

        shell.Setup.Steps.Single(s => s.Id == "recipes").Action!.Open();
        await ShellWait.Until(shell, () => shell.ApproveAll.Phase == ApproveAllPhase.Plan, Token);

        Assert.True(shell.ApproveAll.IsOpen);
        Assert.Empty(_server.Approvals);
    }, Token);

    [Fact]
    public Task Home_hides_a_setup_row_for_a_step_the_card_draws_and_keeps_the_rest() => UiThread.Run(async () =>
    {
        _server.NeedsYouView = RouteAnswer.SpecShaped("needs-you", "extra-setup-rows");
        var shell = await Connected();
        await ShellWait.Until(shell, () => shell.NeedsYou.Seen.Rows.Count > 0 || shell.Setup.DrawnIds.Count > 0, Token);

        Assert.Equal(["map", "mcp", "profile"], shell.Setup.DrawnIds.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["setup:recipes"], shell.NeedsYou.Rows.Select(r => r.Id), StringComparer.Ordinal);   // mcp is on the card, recipes is behind "and 3 more"
    }, Token);

    [Fact]
    public Task A_row_comes_back_to_Home_when_the_card_folds_the_step_away() => UiThread.Run(async () =>
    {
        _server.NeedsYouView = RouteAnswer.SpecShaped("needs-you", "extra-setup-rows");
        var shell = await Connected();
        _server.SetupAnswer = RouteAnswer.SpecShaped("setup", "all-done");

        await shell.Health.RefreshViews(new Uri("http://127.0.0.1:7410/"), Token);
        await ShellWait.Until(shell, () => shell.NeedsYou.Rows.Count == 2, Token);

        Assert.Equal(["setup:mcp", "setup:recipes"], shell.NeedsYou.Rows.Select(r => r.Id).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }, Token);
}
