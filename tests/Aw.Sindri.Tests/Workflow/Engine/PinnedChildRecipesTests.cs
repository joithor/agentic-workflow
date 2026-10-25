using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;
using static Aw.Sindri.Tests.Workflow.Engine.ChildFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>The child recipes an approval pinned (engine P7 plan D2, spec §4.1): a child resolves only at the hash the pins name.</summary>
public sealed class PinnedChildRecipesTests
{
    private static readonly RecipeHash _other = RecipeHash.From("sha256:" + new string('6', 64));

    private static Dictionary<RecipeId, ChildRecipe> Loaded(RecipeHash hash) => new() { [Step] = new ChildRecipe(StepRecipe, hash) };

    [Fact, Trait("Lane", "unit")]
    public void A_child_resolves_only_at_the_pinned_hash()
    {
        var pins = new Dictionary<RecipeId, RecipeHash> { [Step] = StepPin };

        var pinned = new PinnedChildRecipes(Loaded(StepPin), pins).Resolve(Step);
        var drifted = new PinnedChildRecipes(Loaded(_other), pins).Resolve(Step);

        Assert.Equal(new ChildRecipe(StepRecipe, StepPin), pinned);
        Assert.Null(drifted);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_loaded_child_nobody_pinned_and_an_unknown_one_do_not_resolve()
    {
        var unpinned = new PinnedChildRecipes(Loaded(StepPin), new Dictionary<RecipeId, RecipeHash>());
        var nothing = new PinnedChildRecipes(new Dictionary<RecipeId, ChildRecipe>(), new Dictionary<RecipeId, RecipeHash> { [Step] = StepPin });

        Assert.Null(unpinned.Resolve(Step));
        Assert.Null(nothing.Resolve(Step));
        Assert.Null(unpinned.Resolve(RecipeId.From("workflow.nothing")));
    }
}
