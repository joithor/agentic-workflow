using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.ShowRecipe;

/// <summary>The recipe view carries the plain-words fields (setup-once spec §5.3, §7.7): the recipe's displayName, each state's why and displayName, and the transitions that have a label.</summary>
public sealed class RecipeViewsDisplayTests
{
    private static readonly RecipeVersion _stamp = new(RecipeId.From("workflow.minimal"), 1, RecipeHash.From("sha256:" + new string('a', 64)));

    [Fact, Trait("Lane", "unit")]
    public void The_view_takes_its_display_name_from_the_recipe_unless_one_is_given()
    {
        var recipe = ARecipe.Minimal(_stamp.Id);

        Assert.Equal("Produce and finish", RecipeViews.Of(recipe, _stamp).DisplayName);
        Assert.Equal("Given name", RecipeViews.Of(recipe, _stamp, "Given name").DisplayName);
        Assert.Null(RecipeViews.Of(recipe with { DisplayName = null }, _stamp).DisplayName);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_states_why_and_display_name_are_on_its_view_and_the_others_carry_none()
    {
        var recipe = ARecipe.Minimal(_stamp.Id);
        recipe = recipe with
        {
            States = recipe.States.ToDictionary(
                s => s.Key,
                s => s.Key == "produce" ? s.Value with { Why = "An AI writes it.", DisplayName = "Write the draft" } : s.Value,
                StringComparer.Ordinal),
        };

        var states = RecipeViews.Of(recipe, _stamp).States.ToDictionary(s => s.State, StringComparer.Ordinal);

        Assert.Equal(("An AI writes it.", "Write the draft"), (states["produce"].Why, states["produce"].DisplayName));
        Assert.Equal((null, null), (states["done"].Why, states["done"].DisplayName));
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_the_labelled_transitions_are_listed_and_none_leaves_the_field_out()
    {
        var recipe = ARecipe.Minimal(_stamp.Id);
        Assert.Null(RecipeViews.Of(recipe, _stamp).TransitionLabels);

        var labelled = recipe with { Transitions = [recipe.Transitions[0] with { Label = "all done" }] };

        var label = Assert.Single(RecipeViews.Of(labelled, _stamp).TransitionLabels!);
        Assert.Equal(["produce"], label.From, StringComparer.Ordinal);
        Assert.Equal(("done", "step.completed", "all done"), (label.To, label.On, label.Label));
    }
}
