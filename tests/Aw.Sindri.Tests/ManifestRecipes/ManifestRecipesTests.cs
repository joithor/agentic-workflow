using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Features.ManifestRecipes;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.ManifestRecipes;

/// <summary>'recipe manifest' (setup-once spec §5.3): <c language="text">id@version → hash</c> and the eval-case count per recipe, with the registry's own hash function, in process.</summary>
public sealed class ManifestRecipesTests : IDisposable
{
    private readonly RecipeDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private static ManifestRecipesService Service() => new(new FileRecipeDirectories(), RecipeReaderTests.Reader);

    private async Task<RecipeManifest> Manifest() =>
        (await Service().Handle(new ManifestRecipesRequest(_directory.Path), TestContext.Current.CancellationToken)).Value;

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_directory.Path, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: a path under the temp directory always has a parent
        File.WriteAllText(path, text);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Each_recipe_is_listed_with_its_version_hash_display_name_and_eval_count_sorted_by_id()
    {
        Write("workflow.zed.yaml", SampleRecipes.Recipe("zed", version: 3, displayName: "Last in order"));
        Write("workflow.alpha.yaml", SampleRecipes.Recipe("alpha", evals: [SampleRecipes.EvalPath("alpha"), SampleRecipes.EvalPath("alpha", "second")]));
        Write(SampleRecipes.EvalPath("alpha"), SampleRecipes.Case());
        Write(SampleRecipes.EvalPath("alpha", "second"), SampleRecipes.Case());

        var manifest = await Manifest();

        Assert.Equal(["workflow.alpha", "workflow.zed"], manifest.Recipes.Select(r => r.Id.Value), StringComparer.Ordinal);
        Assert.Empty(manifest.Invalid);
        var alpha = manifest.Recipes[0];
        Assert.Equal((1, "Produce and finish", 2), (alpha.Version, alpha.DisplayName, alpha.EvalCases));
        Assert.Equal(RecipeReaderTests.Reader.Read(SampleRecipes.Recipe("alpha", evals: [SampleRecipes.EvalPath("alpha"), SampleRecipes.EvalPath("alpha", "second")]), "x.yaml").Document!.Hash.Value, alpha.Hash);
        Assert.Equal((3, "Last in order", 0), (manifest.Recipes[1].Version, manifest.Recipes[1].DisplayName, manifest.Recipes[1].EvalCases));
        Assert.Matches("^sha256:[0-9a-f]{64}$", manifest.Digest);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_digest_is_the_same_for_the_same_set_whatever_order_the_files_were_written_in_and_moves_with_a_version()
    {
        Write("workflow.a.yaml", SampleRecipes.Recipe("a"));
        Write("workflow.b.yaml", SampleRecipes.Recipe("b"));
        var first = await Manifest();
        using var other = new RecipeDirectory();
        await File.WriteAllTextAsync(Path.Combine(other.Path, "workflow.b.yaml"), SampleRecipes.Recipe("b"), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(other.Path, "workflow.a.yaml"), SampleRecipes.Recipe("a"), TestContext.Current.CancellationToken);

        var second = (await Service().Handle(new ManifestRecipesRequest(other.Path), TestContext.Current.CancellationToken)).Value;
        await File.WriteAllTextAsync(Path.Combine(other.Path, "workflow.b.yaml"), SampleRecipes.Recipe("b", version: 2), TestContext.Current.CancellationToken);
        var bumped = (await Service().Handle(new ManifestRecipesRequest(other.Path), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(first.Digest, second.Digest, StringComparer.Ordinal);
        Assert.NotEqual(first.Digest, bumped.Digest, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_eval_file_change_changes_the_recipes_evals_hash_and_the_digest_but_not_its_hash_or_another_recipes_entry()
    {
        Write("workflow.a.yaml", SampleRecipes.Recipe("a", evals: [SampleRecipes.EvalPath("a")]));
        Write("workflow.b.yaml", SampleRecipes.Recipe("b", evals: [SampleRecipes.EvalPath("b")]));
        Write(SampleRecipes.EvalPath("a"), SampleRecipes.Case());
        Write(SampleRecipes.EvalPath("b"), SampleRecipes.Case());
        var before = await Manifest();

        Write(SampleRecipes.EvalPath("a"), SampleRecipes.Case("produce"));
        var after = await Manifest();

        Assert.Equal(before.Recipes[0].Hash, after.Recipes[0].Hash);
        Assert.NotEqual(before.Recipes[0].EvalsHash, after.Recipes[0].EvalsHash, StringComparer.Ordinal);
        Assert.Equal(before.Recipes[1], after.Recipes[1]);
        Assert.NotEqual(before.Digest, after.Digest, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_recipe_that_does_not_read_is_listed_as_invalid_the_others_are_kept_and_there_is_no_digest()
    {
        Write("workflow.good.yaml", SampleRecipes.Recipe("good"));
        Write("workflow.bad.yaml", "schema: 1\nid: workflow.bad\nretries: 3\n");

        var manifest = await Manifest();

        Assert.Equal(["workflow.good"], manifest.Recipes.Select(r => r.Id.Value), StringComparer.Ordinal);
        var invalid = Assert.Single(manifest.Invalid);
        Assert.Equal("workflow.bad.yaml", invalid.File);
        Assert.StartsWith("RCP-", invalid.Finding, StringComparison.Ordinal);
        Assert.Null(manifest.Digest);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_recipe_without_a_display_name_is_listed_with_none_and_as_invalid_so_there_is_no_digest()
    {
        Write("workflow.plain.yaml", SampleRecipes.Recipe("plain", displayName: null));

        var manifest = await Manifest();

        Assert.Null(Assert.Single(manifest.Recipes).DisplayName);
        var invalid = Assert.Single(manifest.Invalid);
        Assert.Equal("workflow.plain.yaml", invalid.File);
        Assert.StartsWith("RCP-DISPLAY-001:", invalid.Finding, StringComparison.Ordinal);
        Assert.Null(manifest.Digest);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Files_that_are_not_recipes_are_left_out()
    {
        Write("workflow.only.yaml", SampleRecipes.Recipe("only"));
        Write("README.md", "# Recipes\n");
        Write("notes/workflow.hidden.yaml", SampleRecipes.Recipe("hidden"));

        var manifest = await Manifest();

        Assert.Equal(["workflow.only"], manifest.Recipes.Select(r => r.Id.Value), StringComparer.Ordinal);
        Assert.Empty(manifest.Invalid);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_directory_that_does_not_exist_is_SND_RECIPE_010()
    {
        var result = await Service().Handle(new ManifestRecipesRequest(Path.Combine(_directory.Path, "nope")), TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowErrors.RecipeUnreadable.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_pure_builder_gives_what_the_service_gives_for_the_same_files()
    {
        var files = new[]
        {
            new RecipeSetFile("workflow.b.yaml", Encoding.UTF8.GetBytes(SampleRecipes.Recipe("b", evals: [SampleRecipes.EvalPath("b")]))),
            new RecipeSetFile(SampleRecipes.EvalPath("b"), Encoding.UTF8.GetBytes(SampleRecipes.Case())),
            new RecipeSetFile("workflow.a.yaml", Encoding.UTF8.GetBytes(SampleRecipes.Recipe("a"))),
        };

        var manifest = RecipeManifests.Build(files, RecipeReaderTests.Reader);

        Assert.Equal([("workflow.a", 0), ("workflow.b", 1)], manifest.Recipes.Select(r => (r.Id.Value, r.EvalCases)));
        var shuffled = RecipeManifests.Build([files[2], files[0], files[1]], RecipeReaderTests.Reader);
        Assert.Equal(manifest.Recipes, shuffled.Recipes);
        Assert.Equal(manifest.Digest, shuffled.Digest, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_json_shape_is_recipes_invalid_and_digest_with_the_entry_fields_api_reads()
    {
        var entry = new ManifestEntry(RecipeId.From("workflow.build"), 3, "sha256:" + new string('a', 64), "Build one phase", 3, "sha256:" + new string('b', 64));
        var manifest = new RecipeManifest([entry], [new ManifestInvalid("workflow.x.yaml", "RCP-SCHEMA-001: bad")], Digest: null);

        var bytes = AwJson.Serialize(manifest, SliceHarness.Json);
        var json = Encoding.UTF8.GetString(bytes);

        Assert.Equal(
            "{\"recipes\":[{\"id\":\"workflow.build\",\"version\":3,\"hash\":\"sha256:" + new string('a', 64) + "\",\"displayName\":\"Build one phase\",\"evalCases\":3,\"evalsHash\":\"sha256:" + new string('b', 64) + "\"}],"
            + "\"invalid\":[{\"file\":\"workflow.x.yaml\",\"finding\":\"RCP-SCHEMA-001: bad\"}],\"digest\":null}",
            json);
        Assert.Equal(manifest.Recipes, AwJson.Deserialize<RecipeManifest>(bytes, SliceHarness.Json).Value.Recipes);
        _ = JsonDocument.Parse(json);
    }
}
