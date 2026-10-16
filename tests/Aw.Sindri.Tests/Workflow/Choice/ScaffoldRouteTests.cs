using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Workflow.Choice;

/// <summary>'recipe eval --scaffold' through a route (DT spec §8.1): a case per path, each choice taken in turn.</summary>
public sealed class ScaffoldRouteTests
{
    [Fact, Trait("Lane", "unit")]
    public void Scaffold_walks_every_choice_of_a_route()
    {
        var recipe = EvalComposition.Recipe(RouteRecipes.Dev);

        var cases = EvalScaffold.GenerateAll(recipe, EvalComposition.Catalog, NoChildren);

        Assert.Equal(
            ["scaffold-ui", "scaffold-api", "scaffold-db", "scaffold-all", "scaffold-none", "scaffold-other", "scaffold-uncertain", "scaffold-step_failed"],
            cases.Select(c => c.Name), StringComparer.Ordinal);
        foreach (var evalCase in cases)
        {
            var verdict = EvalRunner.Run(evalCase, recipe, EvalComposition.Catalog, NoChildren);
            Assert.True(verdict.Passed, $"{evalCase.Name}: {verdict.Detail}");
        }

        Assert.Equal("evidence-other", cases[5].Steps[0].Expect.State);
        Assert.Equal("route-evidence-desk", cases[6].Steps[0].Expect.State);
        Assert.Equal("route-evidence-desk", cases[7].Steps[0].Expect.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_choice_named_failed_and_the_step_failed_path_get_different_case_names()
    {
        var yaml = RouteRecipes.Edit(RouteRecipes.Edit(RouteRecipes.Compact, "\"api: a contract changed\"", "\"failed: a contract failed\""), "outcome: api }", "outcome: failed }");
        var document = RecipeComposition.ReadClean(yaml, "route.yaml");

        var cases = EvalScaffold.GenerateAll(document.Recipe with { Hash = document.Hash }, EvalComposition.Catalog, NoChildren);

        Assert.Equal(["scaffold-ui", "scaffold-failed", "scaffold-uncertain", "scaffold-step_failed"], cases.Select(c => c.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_routes_whose_choices_spell_the_failure_path_still_get_distinct_case_names()
    {
        var document = RecipeComposition.ReadClean(TwoRoutes, "two-routes.yaml");

        var cases = EvalScaffold.GenerateAll(document.Recipe with { Hash = document.Hash }, EvalComposition.Catalog, NoChildren);

        var names = cases.Select(c => c.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("scaffold-step-failed", names, StringComparer.Ordinal);        // route A picks 'step', route B picks 'failed'
        Assert.Contains("scaffold-step_failed", names, StringComparer.Ordinal);        // route A's own step.failed path
    }

    private static string TwoRoutes { get; } = RecipeFixtures.Read("valid/workflow.two-routes.yaml");

    [Fact, Trait("Lane", "unit")]
    public void Scaffold_of_a_recipe_without_a_route_is_unchanged()
    {
        var recipe = EvalComposition.Recipe("sindri/workflow.step.yaml");

        var all = EvalScaffold.GenerateAll(recipe, EvalComposition.Catalog, NoChildren);

        var only = Assert.Single(all);
        Assert.Equal("scaffold", only.Name);
        Assert.Equal(
            EvalLines.Write(EvalScaffold.Generate(recipe, EvalComposition.Catalog, NoChildren), EvalComposition.Options),
            EvalLines.Write(only, EvalComposition.Options));
    }

    private static IChildRecipeResolver NoChildren { get; } = InMemoryChildRecipes.None;
}
