using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Setup;
using static Aw.Sindri.Ui.Tests.Setup.SetupFixture;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The one action a step offers: an app button when the app can do it, otherwise the terminal command with Copy.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupActionTests
{
    private readonly SetupFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SetupStepViewModel Step(SetupViewModel setup, string id) => setup.Steps.Single(s => s.Id == id);

    [Fact]
    public async Task A_step_with_no_action_has_none()
    {
        _f.Serve("first-run");

        var step = Step(await _f.Read(Token), "profile");

        Assert.Null(step.Action);
    }

    [Fact]
    public async Task Approve_all_is_an_app_button_when_the_app_can_open_it()
    {
        _f.Serve(With("first-run", "recipes", "needs-you", "ApproveAllRecipes"));
        _ = _f.InApp.Add("ApproveAllRecipes");
        var setup = await _f.Read(Token);
        var asked = new List<string>();
        setup.ActionRequested += (_, e) => asked.Add(e.Action);

        var action = Step(setup, "recipes").Action!;
        action.Open();

        Assert.Equal(("Approve all workflows", true, null), (action.Label, action.IsInApp, action.Cli));
        Assert.Equal("Approve all workflows, Approve the recipes", action.Name);
        Assert.Equal(["ApproveAllRecipes"], asked, StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_action_the_app_cannot_open_shows_the_catalogs_command_with_Copy()
    {
        _f.Serve(With("first-run", "recipes", "needs-you", "ApproveAllRecipes"));

        var action = Step(await _f.Read(Token), "recipes").Action!;

        Assert.False(action.IsInApp);
        Assert.Equal("sindri recipe approve-all", action.Cli?.Cli);   // the catalog's own CLI form wins over the app's table
        Assert.Equal("Approve all workflows", action.Label);
        await action.Cli!.Copy(Token);
        Assert.Equal("sindri recipe approve-all", _f.Clipboard.Text);
    }

    [Fact]
    public async Task An_action_the_catalog_does_not_list_shows_the_spec_command()
    {
        _f.Serve("first-run");

        var action = Step(await _f.Read(Token), "repo").Action!;

        Assert.Equal(("Add a repo", "sindri-app repo add <path>", false), (action.Label, action.Cli?.Cli, action.IsInApp));
    }

    [Fact]
    public async Task An_action_on_a_done_step_reads_again_or_another()
    {
        _f.Serve(With("first-run", "repo", "done", "AddRepo"));

        var action = Step(await _f.Read(Token), "repo").Action!;

        Assert.Equal("Add another repo", action.Label);
    }

    [Fact]
    public async Task An_action_this_build_does_not_know_is_not_offered()
    {
        _f.Serve(With("first-run", "repo", "needs-you", "ReticulateSplines"));

        var step = Step(await _f.Read(Token), "repo");

        Assert.Null(step.Action);
    }
}
