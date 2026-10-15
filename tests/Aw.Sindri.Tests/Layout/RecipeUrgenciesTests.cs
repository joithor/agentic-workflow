using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.Layout;

/// <summary>Where the engine reads a recipe's urgency (UI screens rev 8 E-21): the recipe's word, and work for anything that isn't one.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipeUrgenciesTests
{
    private static InterpretedRecipe With(string urgency) =>
        StatusCases.Recipe with { Recipe = StatusCases.Recipe.Recipe with { Urgency = urgency } };

    [Theory]
    [InlineData("incident", RecipeUrgency.Incident)]
    [InlineData("triage", RecipeUrgency.Triage)]
    [InlineData("work", RecipeUrgency.Work)]
    public void A_recipes_urgency_word_is_its_urgency(string word, RecipeUrgency expected) =>
        Assert.Equal(expected, RecipeUrgencies.UrgencyOf(With(word)));

    [Theory]
    [InlineData("urgent")]
    [InlineData("Incident")]
    [InlineData("")]
    public void A_word_that_is_not_an_urgency_is_work(string word) =>
        Assert.Equal(RecipeUrgency.Work, RecipeUrgencies.UrgencyOf(With(word)));
}
