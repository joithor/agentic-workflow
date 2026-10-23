using System.Globalization;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Tests.ApproveAll;

namespace Aw.Sindri.Tests.Setup;

/// <summary>What the setup list counts of the registered recipes: a stale approval still needs approving again.</summary>
public sealed class SetupRecipesTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_stale_approved_recipe_counts_as_needing_approval()
    {
        var current = Line("workflow.step", 1, RecipeStanding.Approved, []);
        var stale = Line("workflow.item", 1, RecipeStanding.Approved, [StaleReason.PinDrift]);
        var draft = Line("workflow.note", 1, RecipeStanding.Unapproved, []);

        var fact = SetupRecipes.Count([current, stale, draft]);

        Assert.Equal(new RecipesFact(3, 1), fact);
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_the_highest_version_of_a_recipe_counts_and_a_revoked_one_is_left_out()
    {
        var older = Line("workflow.step", 1, RecipeStanding.Approved, []);
        var newer = Line("workflow.step", 2, RecipeStanding.Unapproved, []);
        var revoked = Line("workflow.item", 1, RecipeStanding.Revoked, []);

        Assert.Equal(new RecipesFact(1, 0), SetupRecipes.Count([older, newer, revoked]));
    }

    private static RecipeSummary Line(string id, int version, RecipeStanding standing, IReadOnlyList<StaleReason> stale) =>
        new(RecipeId.From(id), version, ApproveAllFixtures.Hash(version.ToString(CultureInfo.InvariantCulture) + (id.Length % 10).ToString(CultureInfo.InvariantCulture) + "a"), standing, standing == RecipeStanding.Approved ? ApprovalVia.Desk : null, stale, [], "work", [], "path", [], []);
}
