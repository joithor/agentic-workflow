using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>'recipe eval --scaffold' over the shipped block recipes (DT-R2 plan U7): every generated case passes the runner.</summary>
public sealed class ShippedRecipeScaffoldTests
{
    public static TheoryData<string> Recipes => [.. ShippedRecipes.Recipes.Where(r => r.StartsWith("sindri/", StringComparison.Ordinal))];

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Every_scaffolded_case_of_a_shipped_recipe_passes(string path)
    {
        var recipe = EvalComposition.Recipe(path);
        var children = InMemoryChildRecipes.None;
        foreach (var sibling in ShippedRecipes.Recipes.Where(r => r.StartsWith("sindri/", StringComparison.Ordinal)))
        {
            var child = EvalComposition.Recipe(sibling);
            _ = children.With(new ChildRecipe(child, child.Hash!.Value));
        }

        var cases = EvalScaffold.GenerateAll(recipe, EvalComposition.Catalog, children);

        Assert.NotEmpty(cases);
        foreach (var evalCase in cases)
        {
            var verdict = EvalRunner.Run(evalCase, recipe, EvalComposition.Catalog, children);
            Assert.True(verdict.Passed, $"{path} {evalCase.Name}: line {verdict.Line?.ToString(CultureInfo.InvariantCulture)}: {verdict.Detail}");
        }
    }
}
