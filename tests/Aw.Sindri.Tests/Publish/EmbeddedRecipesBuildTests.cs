using System.Diagnostics;
using System.Text.Json;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Publish;

/// <summary>
/// What the Host project embeds as recipes (setup-once spec §5.3): the files of the staged directory <c>AwRecipesDir</c> and nothing else.
/// MSBuild evaluates the project (no compile), so the names are the ones the assembly would carry.
/// </summary>
public sealed class EmbeddedRecipesBuildTests
{
    private const string Prefix = "aw-recipes/sindri/";

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public void An_ordinary_build_embeds_no_recipes()
    {
        Assert.Empty(RecipeResources(stagedDir: null));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public void An_empty_staged_directory_embeds_nothing()
    {
        using var home = new TempHome();
        var staged = Path.Combine(home.Path, "recipes", "sindri");
        _ = Directory.CreateDirectory(staged);

        Assert.Empty(RecipeResources(staged));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public void A_staged_directory_is_embedded_by_relative_path_without_the_readme()
    {
        using var home = new TempHome();
        var staged = Path.Combine(home.Path, "recipes", "sindri");
        Write(staged, "workflow.line.yaml");
        Write(staged, "README.md");
        Write(staged, "evals/workflow.line/happy.jsonl");
        Write(staged, "evals/workflow.line/deep/nested.jsonl");

        Assert.Equal(
            [
                Prefix + "evals/workflow.line/deep/nested.jsonl",
                Prefix + "evals/workflow.line/happy.jsonl",
                Prefix + "workflow.line.yaml",
            ],
            RecipeResources(staged));
    }

    private static void Write(string root, string relative)
    {
        var path = Path.Combine(root, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x\n");
    }

    private static string[] RecipeResources(string? stagedDir)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(Path.Combine(RepoRoot(), "src", "Sindri", "Aw.Sindri.Host", "Aw.Sindri.Host.csproj"));
        start.ArgumentList.Add("-getItem:EmbeddedResource");
        if (stagedDir is not null)
        {
            start.ArgumentList.Add("-p:AwRecipesDir=" + stagedDir);
        }

        using var process = Process.Start(start)!;   // why: the program is dotnet, which evaluates the project
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output.Result + error.Result);

        using var document = JsonDocument.Parse(output.Result);
        return document.RootElement.GetProperty("Items").GetProperty("EmbeddedResource").EnumerateArray()
            .Select(item => item.TryGetProperty("LogicalName", out var name) ? name.GetString() ?? "" : "")
            .Where(name => name.StartsWith("aw-recipes/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test assembly");
    }
}
