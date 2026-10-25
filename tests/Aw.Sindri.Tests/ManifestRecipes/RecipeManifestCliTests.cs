using System.Text.Json;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ManifestRecipes;

/// <summary>
/// 'sindri recipe manifest &lt;dir&gt; [--json]', 'recipe eval --shipped [--json]' and 'recipe validate &lt;file&gt; --against &lt;manifest.json&gt;' through the recipe command table (setup-once spec §5.3):
/// in process, no serve, nothing written to the home. Exit 0 ok, 1 an invalid recipe or a failing case, 2 usage or tool error.
/// </summary>
public sealed class RecipeManifestCliTests : IDisposable
{
    private readonly TempHome _work = new();
    private readonly TempHome _home = new();

    public void Dispose()
    {
        _work.Dispose();
        _home.Dispose();
    }

    private string Put(string relative, string text)
    {
        var path = Path.Combine(_work.Path, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: a path under the temp directory always has a parent
        File.WriteAllText(path, text);
        return path;
    }

    private async Task<CliRun> Run(IEmbeddedRecipes? embedded, params string[] args)
    {
        var collection = Composition.Compose(new ServiceCollection(), name => name == "AW_SINDRI_HOME" ? _home.Path : null, new FakeTimeProvider());
        if (embedded is not null)
        {
            _ = collection.AddSingleton(embedded);
        }

        await using var provider = collection.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var output = new Output(stdout, stderr, scope.ServiceProvider.GetRequiredService<JsonSerializerOptions>());

        var exit = await RecipeCommandTable.Run(args, scope.ServiceProvider, output, TestContext.Current.CancellationToken);

        Assert.Empty(Directory.EnumerateFileSystemEntries(_home.Path));
        return new CliRun(exit, stdout.ToString(), stderr.ToString());
    }

    private void TwoRecipes()
    {
        _ = Put("set/workflow.beta.yaml", SampleRecipes.Recipe("beta", evals: SampleRecipes.EvalPath("beta")));
        _ = Put("set/" + SampleRecipes.EvalPath("beta"), SampleRecipes.Case());
        _ = Put("set/workflow.alpha.yaml", SampleRecipes.Recipe("alpha", version: 2));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Manifest_prints_one_line_per_recipe_sorted_by_id_then_the_digest()
    {
        TwoRecipes();

        var run = await Run(embedded: null, "manifest", Path.Combine(_work.Path, "set"));

        Assert.Equal(0, run.Exit);
        var lines = run.Stdout.TrimEnd('\n').Split('\n');
        Assert.Matches("^workflow\\.alpha@2 → sha256:[0-9a-f]{64} \\(0 eval cases\\)$", lines[0]);
        Assert.Matches("^workflow\\.beta@1 → sha256:[0-9a-f]{64} \\(1 eval case\\)$", lines[1]);
        Assert.Matches("^digest sha256:[0-9a-f]{64}$", lines[2]);
        Assert.Equal(3, lines.Length);
        Assert.Equal(string.Empty, run.Stderr);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Manifest_json_is_one_document_with_recipes_invalid_and_digest()
    {
        TwoRecipes();

        var run = await Run(embedded: null, "manifest", Path.Combine(_work.Path, "set"), "--json");

        Assert.Equal(0, run.Exit);
        using var json = JsonDocument.Parse(run.Stdout);
        var root = json.RootElement;
        Assert.Equal(["recipes", "invalid", "digest"], root.EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
        var beta = root.GetProperty("recipes")[1];
        Assert.Equal(["id", "version", "hash", "displayName", "evalCases", "evalsHash"], beta.EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
        Assert.Equal(("workflow.beta", 1, "Produce and finish", 1), (beta.GetProperty("id").GetString(), beta.GetProperty("version").GetInt32(), beta.GetProperty("displayName").GetString(), beta.GetProperty("evalCases").GetInt32()));
        Assert.Equal(0, root.GetProperty("invalid").GetArrayLength());
        Assert.Matches("^sha256:[0-9a-f]{64}$", root.GetProperty("digest").GetString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Manifest_exits_1_when_a_recipe_is_invalid_and_still_prints_the_rest()
    {
        TwoRecipes();
        _ = Put("set/workflow.bad.yaml", "schema: 1\nid: workflow.bad\nretries: 3\n");

        var text = await Run(embedded: null, "manifest", Path.Combine(_work.Path, "set"));
        var json = await Run(embedded: null, "manifest", Path.Combine(_work.Path, "set"), "--json");

        Assert.Equal(1, text.Exit);
        Assert.Contains("workflow.alpha@2 → ", text.Stdout, StringComparison.Ordinal);
        Assert.Contains("invalid workflow.bad.yaml: RCP-", text.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("digest ", text.Stdout, StringComparison.Ordinal);
        Assert.Equal(1, json.Exit);
        using var document = JsonDocument.Parse(json.Stdout);
        Assert.Equal("workflow.bad.yaml", document.RootElement.GetProperty("invalid")[0].GetProperty("file").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("digest").ValueKind);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("manifest")]
    [InlineData("manifest", "--json")]
    [InlineData("manifest", "dir-a", "dir-b")]
    [InlineData("manifest", "dir-a", "--verbose")]
    public async Task Manifest_without_exactly_one_directory_is_a_usage_error(params string[] args)
    {
        var run = await Run(embedded: null, args);

        Assert.Equal(2, run.Exit);
        Assert.Contains("usage: sindri recipe manifest <dir> [--json]", run.Stderr, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Manifest_of_a_directory_that_does_not_exist_exits_2()
    {
        var run = await Run(embedded: null, "manifest", Path.Combine(_work.Path, "nope"));

        Assert.Equal(2, run.Exit);
        Assert.Contains("SND-RECIPE-010", run.Stderr, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Eval_shipped_with_an_empty_embedded_set_passes_and_says_so()
    {
        var text = await Run(FakeEmbeddedRecipes.Empty, "eval", "--shipped");
        var json = await Run(FakeEmbeddedRecipes.Empty, "eval", "--shipped", "--json");

        Assert.Equal((0, "no recipes are embedded in this build; nothing to evaluate\n"), (text.Exit, text.Stdout));
        Assert.Equal(0, json.Exit);
        Assert.Equal("{\"passed\":true,\"empty\":true,\"recipes\":[]}", json.Stdout.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Eval_shipped_prints_each_recipe_and_its_cases_and_exits_0_when_all_pass()
    {
        var embedded = FakeEmbeddedRecipes.Of(
            ("workflow.beta.yaml", SampleRecipes.Recipe("beta", evals: SampleRecipes.EvalPath("beta"))), (SampleRecipes.EvalPath("beta"), SampleRecipes.Case()));

        var run = await Run(embedded, "eval", "--shipped");

        Assert.Equal(0, run.Exit);
        Assert.Equal("workflow.beta@1\n  happy pass\n1 recipe passed (1 case)\n", run.Stdout);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Eval_shipped_exits_1_with_the_failing_case_line_and_json_carries_the_recipes()
    {
        var embedded = FakeEmbeddedRecipes.Of(
            ("workflow.beta.yaml", SampleRecipes.Recipe("beta", evals: SampleRecipes.EvalPath("beta"))), (SampleRecipes.EvalPath("beta"), SampleRecipes.Case("produce")));

        var text = await Run(embedded, "eval", "--shipped");
        var json = await Run(embedded, "eval", "--shipped", "--json");

        Assert.Equal(1, text.Exit);
        Assert.Contains("  happy fail at " + SampleRecipes.EvalPath("beta") + ":2: ", text.Stdout, StringComparison.Ordinal);
        Assert.EndsWith("1 case failed in 1 recipe\n", text.Stdout, StringComparison.Ordinal);
        Assert.Equal(1, json.Exit);
        using var document = JsonDocument.Parse(json.Stdout);
        var root = document.RootElement;
        Assert.Equal(["passed", "empty", "recipes"], root.EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
        var recipe = root.GetProperty("recipes")[0];
        Assert.Equal(["id", "version", "hash", "displayName", "passed", "cases"], recipe.EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
        Assert.Equal(["case", "passed", "line", "detail"], recipe.GetProperty("cases")[0].EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("--shipped", "somefile.yaml")]
    [InlineData("--json", "--shipped", "--scaffold")]
    public async Task Eval_shipped_takes_only_json_beside_it(params string[] args)
    {
        var run = await Run(FakeEmbeddedRecipes.Empty, ["eval", .. args]);

        Assert.Equal(2, run.Exit);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Validate_against_a_manifest_flags_a_changed_recipe_at_the_same_version_and_exits_1()
    {
        _ = Put("main/workflow.a.yaml", SampleRecipes.Recipe("a"));
        var manifest = await Run(embedded: null, "manifest", Path.Combine(_work.Path, "main"), "--json");
        var baseline = Put("baseline.json", manifest.Stdout);
        var changed = Put("workflow.a.yaml", SampleRecipes.Recipe("a", displayName: "A different name"));
        var same = Put("same/workflow.a.yaml", SampleRecipes.Recipe("a"));

        var bad = await Run(embedded: null, "validate", changed, "--against", baseline);
        var good = await Run(embedded: null, "validate", same, "--against", baseline);

        Assert.Equal(1, bad.Exit);
        Assert.Contains("RCP-VERSION-001: workflow.a version 1 is registered with hash ", bad.Stdout, StringComparison.Ordinal);
        Assert.Equal(0, good.Exit);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Validate_against_needs_a_manifest_file()
    {
        var file = Put("workflow.a.yaml", SampleRecipes.Recipe("a"));

        var run = await Run(embedded: null, "validate", file, "--against");

        Assert.Equal(2, run.Exit);
        Assert.Contains("usage: sindri recipe validate <file> [--json] [--against <manifest.json>]", run.Stderr, StringComparison.Ordinal);
    }

    private sealed record CliRun(int Exit, string Stdout, string Stderr);
}
