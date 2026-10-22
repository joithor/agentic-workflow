using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Stats;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.PrBabysitter;

/// <summary>The babysitter stats key on the recipe's state and command names (<see cref="BabysitterNames"/>): a rename in 'workflow.pr-babysitter' fails here instead of silently zeroing a figure.</summary>
[Lane(TestLane.Unit)]
public sealed class BabysitterNamesTests
{
    private static readonly Aw.Sindri.Core.Workflow.Recipes.Recipe _recipe =
        RecipeReaderTests.Reader.Read(ShippedRecipes.Read("sindri/workflow.pr-babysitter.yaml"), "workflow.pr-babysitter.yaml").Document!.Recipe;   // why: the shipped recipes bind (ShippedRecipesTests)

    [Fact]
    public void The_recipe_id_is_the_one_the_stats_name() => Assert.Equal(BabysitterNames.Recipe, _recipe.Id.Value);

    [Theory]
    [InlineData(BabysitterNames.Ready)]
    [InlineData(BabysitterNames.Merged)]
    [InlineData(BabysitterNames.Snapshot)]
    [InlineData(BabysitterNames.CiFix)]
    [InlineData(BabysitterNames.Recheck)]
    [InlineData(BabysitterNames.RecheckOwner)]
    [InlineData(BabysitterNames.DocsMerging)]
    [InlineData(BabysitterNames.DocsMergeOwner)]
    [InlineData(BabysitterNames.MergingFromReady)]
    [InlineData(BabysitterNames.ReworkNotes)]
    [InlineData(BabysitterNames.HeldNext)]
    [InlineData(BabysitterNames.Docs)]
    public void Every_state_the_stats_key_on_exists_in_the_recipe(string state) =>
        Assert.True(_recipe.States.ContainsKey(state), $"workflow.pr-babysitter has no state '{state}' (the babysitter stats key on it: update BabysitterNames and the counter)");

    [Fact]
    public void Each_owner_verb_leaves_ready_for_the_state_the_stats_name()
    {
        foreach (var (verb, to) in BabysitterNames.OwnerVerbs)
        {
            Assert.Contains(_recipe.Transitions, t => t.From.Contains(BabysitterNames.Ready, StringComparer.Ordinal) && t.On == "command." + verb && t.To == to);
        }
    }
}
