using System.Globalization;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.ManifestRecipes;

/// <summary>
/// 'recipe validate --against &lt;manifest.json&gt;' (setup-once spec §5.3): the rule "a content change bumps the version". The manifest is the saved output of 'recipe manifest --json'
/// over origin/main; a recipe whose hash changed at the same version is RCP-VERSION-001, one below the highest is RCP-VERSION-002, and a new recipe passes.
/// </summary>
public sealed class ValidateAgainstTests : IDisposable
{
    private readonly RecipeDirectory _directory = new();
    private readonly IRecipeFiles _files = new FileRecipeFiles();

    public void Dispose() => _directory.Dispose();

    private ValidateRecipeService Service() =>
        new(RecipeReaderTests.Reader, _files, FixtureCatalog.Shipped(), new ApprovedRecipeProfileFacts(new InMemoryProfiles()));

    /// <summary>Writes the manifest of <paramref name="recipes"/> (as origin/main had them) and returns its path.</summary>
    private string Baseline(params string[] recipes)
    {
        var files = recipes.Select((text, i) => new RecipeSetFile($"workflow.r{i.ToString(CultureInfo.InvariantCulture)}.yaml", Encoding.UTF8.GetBytes(text))).ToList();
        var manifest = RecipeManifests.Build(files, RecipeReaderTests.Reader);
        return _directory.Write("baseline.json", Encoding.UTF8.GetString(AwJson.Serialize(manifest, SliceHarness.Json)));
    }

    private async Task<ValidateRecipeResponse> Validate(string recipe, string baseline)
    {
        var path = _directory.Write("candidate.yaml", recipe);
        return (await Service().Handle(ValidateRecipeRequest.FromPath(path) with { Against = baseline }, TestContext.Current.CancellationToken)).Value;
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_same_recipe_at_the_same_version_with_the_same_hash_passes()
    {
        var baseline = Baseline(SampleRecipes.Recipe("a"));

        var response = await Validate(SampleRecipes.Recipe("a"), baseline);

        Assert.True(response.Passed, string.Join('\n', response.Findings.Select(f => f.ToLine())));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_changed_recipe_at_the_same_version_is_RCP_VERSION_001()
    {
        var baseline = Baseline(SampleRecipes.Recipe("a"));

        var response = await Validate(SampleRecipes.Recipe("a", displayName: "A different name"), baseline);

        Assert.False(response.Passed);
        var finding = Assert.Single(response.Findings, f => f.Rule == "RCP-VERSION-001");
        Assert.StartsWith("RCP-VERSION-001: workflow.a version 1 is registered with hash ", finding.ToLine(), StringComparison.Ordinal);
        Assert.Contains("raise version to 2", finding.ToLine(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_changed_recipe_with_a_raised_version_passes()
    {
        var baseline = Baseline(SampleRecipes.Recipe("a"));

        var response = await Validate(SampleRecipes.Recipe("a", version: 2, displayName: "A different name"), baseline);

        Assert.True(response.Passed, string.Join('\n', response.Findings.Select(f => f.ToLine())));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_version_below_the_highest_in_the_baseline_is_RCP_VERSION_002()
    {
        var baseline = Baseline(SampleRecipes.Recipe("a", version: 3));

        var response = await Validate(SampleRecipes.Recipe("a", version: 2, displayName: "Older words"), baseline);

        Assert.Contains(response.Findings, f => f.Rule == "RCP-VERSION-002");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_recipe_the_baseline_does_not_have_passes()
    {
        var baseline = Baseline(SampleRecipes.Recipe("other"));

        var response = await Validate(SampleRecipes.Recipe("brand-new"), baseline);

        Assert.True(response.Passed, string.Join('\n', response.Findings.Select(f => f.ToLine())));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Without_against_nothing_is_checked_against_a_baseline()
    {
        var path = _directory.Write("candidate.yaml", SampleRecipes.Recipe("a", displayName: "A different name"));

        var response = (await Service().Handle(ValidateRecipeRequest.FromPath(path), TestContext.Current.CancellationToken)).Value;

        Assert.DoesNotContain(response.Findings, f => f.Rule.StartsWith("RCP-VERSION", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_baseline_that_is_missing_or_is_not_a_manifest_is_SND_RECIPE_010()
    {
        var candidate = _directory.Write("candidate.yaml", SampleRecipes.Recipe("a"));
        var notJson = _directory.Write("notes.json", "not json at all");

        var missing = await Service().Handle(ValidateRecipeRequest.FromPath(candidate) with { Against = Path.Combine(_directory.Path, "none.json") }, TestContext.Current.CancellationToken);
        var garbled = await Service().Handle(ValidateRecipeRequest.FromPath(candidate) with { Against = notJson }, TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowErrors.RecipeUnreadable.Code, missing.Error.Code);
        Assert.Equal(WorkflowErrors.RecipeUnreadable.Code, garbled.Error.Code);
    }
}
