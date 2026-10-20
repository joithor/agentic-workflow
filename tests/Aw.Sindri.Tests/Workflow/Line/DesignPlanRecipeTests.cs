using System.Globalization;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>workflow.design and workflow.plan (line spec Appendix C.1, C.2; LN-Y1): shape, loops, evals and the stops.</summary>
public sealed class DesignPlanRecipeTests
{
    private static RecipeDocument Read(string id) =>
        RecipeComposition.ReadClean(ShippedRecipes.Read($"sindri/{id}.yaml"), $"{id}.yaml");

    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow.design")]
    [InlineData("workflow.plan")]
    public void The_recipe_validates_against_the_shipped_catalog_with_no_findings(string id) =>
        Assert.Empty(RecipeComposition.Errors(Read(id), FixtureCatalog.Shipped(), InMemoryChildRecipes.None));

    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow.design", "rounds=5;lens=4;lens-runs=40", 1500000)]
    [InlineData("workflow.plan", "rounds=5;refreeze=2;revisions=5", 1000000)]
    public void The_loop_caps_and_budget_are_the_ones_the_spec_names(string id, string loops, int tokens)
    {
        var recipe = Read(id).Recipe;

        Assert.Equal(loops.Split(';').Order(StringComparer.Ordinal), recipe.Loops.Select(l => $"{l.Key}={l.Value.Cap.ToString(CultureInfo.InvariantCulture)}").Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(tokens, recipe.Budget?.Tokens);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow.design", "designed=resolved;dropped=unresolved")]
    [InlineData("workflow.plan", "planned=resolved;redesign=unresolved;dropped=failed")]
    public void The_terminals_carry_the_outcomes_the_line_reads(string id, string terminals)
    {
        var recipe = Read(id).Recipe;
        var actual = recipe.States.Where(s => s.Value.Kind == StateKind.Terminal).Select(s => $"{s.Key}={s.Value.Outcome?.Value}");

        Assert.Equal(terminals.Split(';').Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow.design", "check-gaps-twice;evidence-incomplete;fresh-happy;lens-cap-accept;lens-cap-drop;lens-findings-revise;revise-after-caps;revise-mode;two-lens-pack")]
    [InlineData("workflow.plan", "assumed-happy;changed-decision;drop;evidence-incomplete;freeze-mismatch;no-questions;objections;revise-cap;revise-plan")]
    public void The_recipe_lists_the_seven_cases_of_appendix_b1_and_two_more(string id, string cases)
    {
        var names = Read(id).Recipe.Evals.Select(static e => Path.GetFileNameWithoutExtension(e));

        Assert.Equal(cases.Split(';').Order(StringComparer.Ordinal), names.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
