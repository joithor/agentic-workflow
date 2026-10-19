using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Tests.Workflow.Choice;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>A Step whose outcomes come from its parameters (desk.choose, judge.choose) shows them in the task and in the complete line, so a CLI driver can tell the valid ones.</summary>
[Lane(TestLane.Unit)]
public sealed class ChoiceStepViewsTests
{
    private static InstanceView View(string state)
    {
        var recipe = EvalComposition.Recipe(RouteRecipes.Dev);
        var interpreted = new InterpretedRecipe(new RecipeVersion(recipe.Id, recipe.Version, recipe.Hash!.Value), recipe, EvalComposition.Catalog, new Dictionary<FactKey, InputFact>());   // why: the shipped recipe is read with its hash
        return InstanceViews.Of(EngineFixture.At(recipe, state), interpreted, DateTimeOffset.UnixEpoch, timeoutAt: null, new InstanceViewContext(Version: 1, Children: null));
    }

    [Fact]
    public void A_desk_choose_task_lists_the_choices_of_its_twin_state()
    {
        var view = View("route-evidence-desk");

        Assert.Equal(["ui", "api", "db", "all", "none", "other"], view.Task!.Outcomes.Select(o => o.Value), StringComparer.Ordinal);
        Assert.Contains("--outcome <ui|api|db|all|none|other>", view.Next, StringComparison.Ordinal);
        Assert.DoesNotContain("--outcome <outcome>", view.Next, StringComparison.Ordinal);
    }

    [Fact]
    public void A_judge_choose_task_lists_its_choices_and_uncertain()
    {
        var view = View("route-evidence");

        Assert.Equal(["ui", "api", "db", "all", "none", "other", "uncertain"], view.Task!.Outcomes.Select(o => o.Value), StringComparer.Ordinal);
        Assert.Contains("--outcome <ui|api|db|all|none|other|uncertain>", view.Next, StringComparison.Ordinal);
    }

    [Fact]
    public void The_complete_step_command_names_every_outcome()
    {
        var view = View("route-evidence-desk");

        var complete = view.AllowedCommands.Single(c => c.Action == "CompleteStep");

        Assert.Contains("--outcome <ui|api|db|all|none|other>", complete.Cli, StringComparison.Ordinal);
    }
}
