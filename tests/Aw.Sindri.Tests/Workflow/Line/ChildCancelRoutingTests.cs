using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>
/// DT-R2 and line spec App. C: every child wait routes 'cancelled' on a transition of its own. Over the whole recipes/
/// tree, so it also guards workflow.bugfix (DT-M). Stricter than a name check: the transition's condition is exactly
/// the outcome, not an 'any' it shares with 'unresolved', and it records nothing (RCP-WIRE-003).
/// </summary>
public sealed class ChildCancelRoutingTests
{
    public static TheoryData<string> Recipes => [.. ShippedRecipes.Recipes];

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Every_child_wait_has_a_transition_of_its_own_for_cancelled(string path)
    {
        var recipe = RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path)).Recipe;

        foreach (var name in recipe.States.Where(s => s.Value.WaitFor?.Child is not null).Select(static s => s.Key))
        {
            var mentioning = recipe.Transitions.Where(t => t.From.Contains(name, StringComparer.Ordinal) && Names(t.When, "cancelled")).ToList();

            var own = Assert.Single(mentioning);
            Assert.Equal([name], own.From);
            Assert.Equal("cancelled", own.When?.Outcome?.Value);   // the condition is the outcome alone: not an 'any' or 'all' that also takes 'unresolved'
            Assert.Empty(own.Produces);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_line_recipes_that_wait_on_children_are_checked()
    {
        var withChildren = ShippedRecipes.Recipes.Where(p => RecipeComposition.ReadClean(ShippedRecipes.Read(p), Path.GetFileName(p)).Recipe.States.Values.Any(s => s.WaitFor?.Child is not null)).ToList();

        Assert.Contains("sindri/workflow.phase.yaml", withChildren, StringComparer.Ordinal);
    }

    private static bool Names(WhenClause? when, string outcome) =>
        when is not null && (when.Outcome?.Value == outcome || (when.All ?? []).Concat(when.Any ?? []).Append(when.Not).Any(c => Names(c, outcome)));
}
