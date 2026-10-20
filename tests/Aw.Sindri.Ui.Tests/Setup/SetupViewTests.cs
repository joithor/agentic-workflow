using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.ViewModels.Setup;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Setup;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The Setup card on Home and the Setup page in the window: rows named for assistive tech, lines as named text, one primary action, and ids and codes behind a closed Details.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SetupViewTests : FrameHost
{
    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnHome(string scenario)
    {
        Server.SetupAnswer = RouteAnswer.SpecShaped("setup", scenario);
        var (window, shell) = await Connected(s => s.Setup.Steps.Count > 0);
        shell.GoTo(PageId.Home);
        Settle(window);
        return (window, shell);
    }

    // The step rows a list draws (the rows are templates, so they carry no names: they are found by their class inside the list).
    private static List<Border> Rows(Window window, string list) =>
        [.. Named<ItemsControl>(window, list).GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("setup-row") && b.IsEffectivelyVisible)];

    [Fact]
    public Task The_card_draws_three_rows_named_in_words_and_says_how_many_it_leaves() => OnUi(async () =>
    {
        var (window, shell) = await OnHome("extra-priority-mix");

        var rows = Rows(window, "SetupCardRows");

        Assert.Equal(3, rows.Count);
        Assert.Equal([.. shell.Setup.CardSteps.Select(s => s.Name)], [.. rows.Select(Heard.Name)], StringComparer.Ordinal);
        Assert.Equal(shell.Setup.Head, Named<TextBlock>(window, "SetupHead").Text);
        Assert.Equal(shell.Setup.Head, Heard.Wrapper(Named<Border>(window, "SetupHeadName")));
        var more = Named<Button>(window, "SetupMore");
        Assert.Equal((true, "Show all steps"), (more.IsEffectivelyVisible, Heard.Name(more)));
        Assert.DoesNotContain(rows, r => Heard.Name(r).Contains("SND-", StringComparison.Ordinal));
    });

    [Fact]
    public Task A_finished_setup_draws_no_card() => OnUi(async () =>
    {
        var (window, _) = await OnHome("all-done");

        Assert.False(Visible(window, "SetupPanel"));
    });

    [Fact]
    public Task Only_the_next_step_carries_an_action_on_the_card() => OnUi(async () =>
    {
        var (window, _) = await OnHome("extra-priority-mix");

        var actions = Named<ItemsControl>(window, "SetupCardRows").GetVisualDescendants().OfType<SetupActionView>().Where(a => a.IsEffectivelyVisible).ToList();

        _ = Assert.Single(actions);   // mcp needs you; its command waits under a hint, nothing else offers one
    });

    [Fact]
    public Task The_press_on_more_shows_every_step_and_the_link_opens_the_page() => OnUi(async () =>
    {
        var (window, shell) = await OnHome("extra-priority-mix");

        Named<Button>(window, "SetupMore").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
        var all = Rows(window, "SetupCardRows").Count;
        Named<Button>(window, "SetupOpen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal((6, PageId.Setup), (all, shell.CurrentPage));
    });

    [Fact]
    public Task The_approve_all_button_is_the_one_accent_action_and_opens_the_dialog() => OnUi(async () =>
    {
        Server.Catalog = RouteAnswer.SpecShaped("commands", "extra-approve-all-ready");
        Server.SetupAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, SetupFixture.With("profile-needs-approval", "recipes", "needs-you", "ApproveAllRecipes").ToJsonString());
        var (window, shell) = await Connected(s => s.Setup.Steps.Count > 0 && s.Health.Catalog.Controls.Count > 0);
        shell.GoTo(PageId.Setup);
        Settle(window);

        var button = Named<Button>(window, "SetupActionButton");
        Assert.Equal("Approve all workflows, Approve the recipes", Heard.Name(button));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.ApproveAll.Phase == ApproveAllPhase.Plan, "the approve-all dialog never opened on the plan");

        Assert.True(shell.ApproveAll.IsOpen);
    });

    [Fact]
    public Task The_page_leads_with_the_next_step_then_lists_every_step_with_details_closed() => OnUi(async () =>
    {
        var (window, shell) = await OnHome("profile-needs-approval");
        shell.GoTo(PageId.Setup);
        Settle(window);

        Assert.Equal(shell.Setup.LeadText, Heard.Wrapper(Named<Border>(window, "SetupLeadName")));
        Assert.Equal("Approve the profile", Heard.Wrapper(Named<Border>(window, "SetupLeadTitle")));
        var rows = Rows(window, "SetupStepRows");
        Assert.Equal([.. shell.Setup.Steps.Select(s => s.Name)], [.. rows.Select(Heard.Name)], StringComparer.Ordinal);
        Assert.False(Named<Expander>(window, "SetupDetails").IsExpanded);
        Assert.Equal(shell.Setup.Head, Heard.Wrapper(Named<Border>(window, "SetupPageHeadName")));
    });

    [Fact]
    public Task Codes_wait_in_Details_and_show_when_it_opens() => OnUi(async () =>
    {
        var (window, shell) = await OnHome("failed");
        shell.GoTo(PageId.Setup);
        Settle(window);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && (t.Text ?? string.Empty).Contains("SND-", StringComparison.Ordinal));

        OpenDisclosures(window);

        Assert.Contains(window.GetVisualDescendants().OfType<Border>(), b => b.DataContext is SetupFieldViewModel && Heard.Name(b) == "Error code: SND-CONTEXT-042");
    });

    [Fact]
    public Task A_step_with_repos_lists_each_repo_as_a_named_line() => OnUi(async () =>
    {
        var (window, shell) = await OnHome("running-progress");
        shell.GoTo(PageId.Setup);
        Settle(window);

        var lines = window.GetVisualDescendants().OfType<Border>().Where(b => b.DataContext is SetupItemViewModel && b.IsEffectivelyVisible).Select(Heard.Name).ToList();

        Assert.Contains("demo, working", lines, StringComparer.Ordinal);
    });
}
