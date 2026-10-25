using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ManifestRecipes;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.EvalShippedRecipes;

/// <summary>'recipe eval --shipped' (setup-once spec §5.3): every embedded recipe's eval cases run over the pure engine, as 'recipe eval &lt;file&gt;' runs one.</summary>
public sealed class EvalShippedRecipesTests
{
    private static EvalShippedRecipesService Service(IEmbeddedRecipes embedded) =>
        new(embedded, RecipeReaderTests.Reader, FixtureCatalog.Shipped(), SliceHarness.Json);

    private static async Task<EvalShippedResponse> Run(IEmbeddedRecipes embedded) =>
        (await Service(embedded).Handle(new EvalShippedRequest(), TestContext.Current.CancellationToken)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_embedded_set_passes_and_says_it_is_empty()
    {
        var response = await Run(FakeEmbeddedRecipes.Empty);

        Assert.True(response.Passed);
        Assert.True(response.Empty);
        Assert.Empty(response.Recipes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_embedded_recipes_cases_run_and_pass_with_the_recipe_identity_on_each_result()
    {
        var response = await Run(FakeEmbeddedRecipes.Of(
            ("workflow.b.yaml", SampleRecipes.Recipe("b", displayName: "Second recipe", evals: SampleRecipes.EvalPath("b"))),
            (SampleRecipes.EvalPath("b"), SampleRecipes.Case()),
            ("workflow.a.yaml", SampleRecipes.Recipe("a", version: 4, evals: [SampleRecipes.EvalPath("a"), SampleRecipes.EvalPath("a", "again")])),
            (SampleRecipes.EvalPath("a"), SampleRecipes.Case()),
            (SampleRecipes.EvalPath("a", "again"), SampleRecipes.Case())));

        Assert.True(response.Passed);
        Assert.False(response.Empty);
        Assert.Equal(["workflow.a", "workflow.b"], response.Recipes.Select(r => r.Recipe), StringComparer.Ordinal);
        var a = response.Recipes[0];
        Assert.Equal((4, "Produce and finish", true), (a.Version!.Value, a.DisplayName, a.Passed));
        Assert.Matches("^sha256:[0-9a-f]{64}$", a.Hash);
        Assert.Equal([SampleRecipes.EvalPath("a"), SampleRecipes.EvalPath("a", "again")], a.Cases.Select(c => c.Case), StringComparer.Ordinal);
        Assert.All(response.Recipes.SelectMany(r => r.Cases), c => Assert.True(c.Passed));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_case_fails_the_run_and_names_its_line_and_what_diverged()
    {
        var response = await Run(FakeEmbeddedRecipes.Of(
            ("workflow.ok.yaml", SampleRecipes.Recipe("ok", evals: SampleRecipes.EvalPath("ok"))),
            (SampleRecipes.EvalPath("ok"), SampleRecipes.Case()),
            ("workflow.off.yaml", SampleRecipes.Recipe("off", evals: SampleRecipes.EvalPath("off"))),
            (SampleRecipes.EvalPath("off"), SampleRecipes.Case("produce"))));

        Assert.False(response.Passed);
        Assert.True(response.Recipes.Single(r => r.Recipe == "workflow.ok").Passed);
        var off = response.Recipes.Single(r => r.Recipe == "workflow.off");
        Assert.False(off.Passed);
        var failed = Assert.Single(off.Cases);
        Assert.Equal((false, 2), (failed.Passed, failed.Line));
        Assert.Contains("done", failed.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_recipe_that_does_not_read_fails_the_run_with_its_error_and_the_others_still_run()
    {
        var response = await Run(FakeEmbeddedRecipes.Of(
            ("workflow.ok.yaml", SampleRecipes.Recipe("ok", evals: SampleRecipes.EvalPath("ok"))),
            (SampleRecipes.EvalPath("ok"), SampleRecipes.Case()),
            ("workflow.bad.yaml", "schema: 1\nid: workflow.bad\nretries: 3\n")));

        Assert.False(response.Passed);
        Assert.True(response.Recipes.Single(r => r.Recipe == "workflow.ok").Passed);
        var bad = response.Recipes.Single(r => r.Recipe == "workflow.bad");
        Assert.False(bad.Passed);
        Assert.Empty(bad.Cases);
        Assert.StartsWith("RCP-", bad.Error, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_declared_eval_file_missing_from_the_set_fails_that_recipe_naming_the_file()
    {
        var response = await Run(FakeEmbeddedRecipes.Of(("workflow.lost.yaml", SampleRecipes.Recipe("lost", evals: SampleRecipes.EvalPath("lost")))));

        Assert.False(response.Passed);
        var lost = Assert.Single(response.Recipes);
        Assert.False(lost.Passed);
        Assert.Contains(SampleRecipes.EvalPath("lost"), lost.Cases.Select(c => c.Detail).Concat([lost.Error]).First(d => d is not null), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Every_shipped_recipes_evals_pass_when_the_checkouts_recipes_are_the_embedded_set()
    {
        var files = ShippedRecipes.Paths
            .Where(p => p.StartsWith("sindri/", StringComparison.Ordinal) && !p.EndsWith("README.md", StringComparison.Ordinal))
            .Select(p => new RecipeSetFile(p["sindri/".Length..], System.Text.Encoding.UTF8.GetBytes(ShippedRecipes.Read(p))))
            .ToList();
        Assert.True(files.Where(f => f.RelativePath.EndsWith(".yaml", StringComparison.Ordinal)).Skip(25).Any(), "the checkout ships 26 recipes");

        var response = await Run(new FakeEmbeddedRecipes(files));

        var failures = response.Recipes.Where(r => !r.Passed).Select(r => r.Recipe + ": " + (r.Error ?? string.Join("; ", r.Cases.Where(c => !c.Passed).Select(c => c.ToLine())))).ToList();
        Assert.Empty(failures);
        Assert.True(response.Passed);
    }
}
