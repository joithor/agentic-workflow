using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The one lookup for a state's words: the recipe's displayName, else the id made readable.</summary>
[Lane(TestLane.Unit)]
public sealed class StateNamesTests
{
    private static RecipeView Recipe(string id, params (string State, string? Name)[] states) =>
        FixtureInstances.Recipe("workflow.project") with
        {
            Id = id,
            States = [.. states.Select(s => new RecipeStateView(s.State, Order: 1, BranchOf: null, Why: null, s.Name))],
        };

    [Fact]
    public void A_recipe_that_names_the_state_gives_its_name() =>
        Assert.Equal("Design the change", StateNames.Of(Recipe("workflow.line", ("design", "Design the change")), "design"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_state_with_no_name_reads_as_its_id_made_readable(string? name) =>
        Assert.Equal("Build And Prove", StateNames.Of(Recipe("workflow.line", ("build-and-prove", name)), "build-and-prove"));

    [Fact]
    public void A_recipe_that_is_not_read_or_lacks_the_state_falls_back_to_the_id()
    {
        Assert.Equal("Design", StateNames.Of(recipe: null, "design"));
        Assert.Equal("Verify", StateNames.Of(Recipe("workflow.line", ("design", "Design the change")), "verify"));
    }

    [Fact]
    public void A_read_recipe_names_its_states_for_every_row_that_asks_by_id()
    {
        var names = new StateNames();
        Assert.Equal("Design", names.Of("workflow.line", "design"));

        var served = names.Add(Recipe("workflow.line", ("design", "Design the change"), ("verify", null)));

        Assert.True(served);
        Assert.Equal(("Design the change", "Verify", "Design"), (names.Of("workflow.line", "design"), names.Of("workflow.line", "verify"), names.Of("workflow.other", "design")));
    }

    [Fact]
    public void A_recipe_that_names_no_state_adds_nothing() =>
        Assert.False(new StateNames().Add(Recipe("workflow.line", ("design", null))));

    [Fact]
    public void A_recipe_is_asked_for_once()
    {
        var names = new StateNames();
        Assert.False(names.WasAsked("workflow.line"));

        names.Asked("workflow.line");

        Assert.True(names.WasAsked("workflow.line"));
    }
}
