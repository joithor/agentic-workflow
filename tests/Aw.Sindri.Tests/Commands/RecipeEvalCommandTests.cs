using System.Text.Json;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// 'sindri recipe eval' as Program runs it, over real files (spec §6.3, W27): case lines and exit 0 or 1, the
/// scaffold, and '--record' reading an instance's events from the ledger (engine P7). It swaps the process's console.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class RecipeEvalCommandTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public RecipeEvalCommandTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    private string Recipe => Path.Combine(_home.Path, "recipes", "workflow.dev-timeout.yaml");

    private string Happy => Path.Combine(_home.Path, "recipes", "evals", "workflow.dev-timeout", "happy.jsonl");

    [Fact, Trait("Lane", "integration")]
    public async Task Passing_evals_print_one_line_per_case_and_exit_0()
    {
        await Put(ShippedRecipes.Read("dev/evals/workflow.dev-timeout/happy.jsonl"));

        Assert.Equal(0, await Run("recipe", "eval", Recipe));
        Assert.Equal("happy pass\n", _out.ToString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_diverging_line_exits_1_and_names_the_file_and_the_line()
    {
        await Put(ShippedRecipes.Read("dev/evals/workflow.dev-timeout/happy.jsonl").Replace("\"state\":\"done\"", "\"state\":\"hold\"", StringComparison.Ordinal));

        Assert.Equal(1, await Run("recipe", "eval", Recipe));
        Assert.Equal("happy fail at evals/workflow.dev-timeout/happy.jsonl:2: expected hold (outcome resolved), got done (outcome resolved)\n", _out.ToString());
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Json_prints_each_cases_result_and_exits_0_when_every_case_passes(bool flagFirst)
    {
        await Put(ShippedRecipes.Read("dev/evals/workflow.dev-timeout/happy.jsonl"));

        Assert.Equal(0, await Run(flagFirst ? ["recipe", "eval", "--json", Recipe] : ["recipe", "eval", Recipe, "--json"]));
        using var body = JsonDocument.Parse(_out.ToString());
        var only = Assert.Single(body.RootElement.GetProperty("cases").EnumerateArray());
        Assert.Equal("evals/workflow.dev-timeout/happy.jsonl", only.GetProperty("case").GetString());
        Assert.True(only.GetProperty("passed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, only.GetProperty("line").ValueKind);
        Assert.Equal(JsonValueKind.Null, only.GetProperty("detail").ValueKind);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Json_names_the_diverging_line_and_exits_1(bool flagFirst)
    {
        await Put(ShippedRecipes.Read("dev/evals/workflow.dev-timeout/happy.jsonl").Replace("\"state\":\"done\"", "\"state\":\"hold\"", StringComparison.Ordinal));

        Assert.Equal(1, await Run(flagFirst ? ["recipe", "eval", "--json", Recipe] : ["recipe", "eval", Recipe, "--json"]));
        using var body = JsonDocument.Parse(_out.ToString());
        var only = Assert.Single(body.RootElement.GetProperty("cases").EnumerateArray());
        Assert.Equal("evals/workflow.dev-timeout/happy.jsonl", only.GetProperty("case").GetString());
        Assert.False(only.GetProperty("passed").GetBoolean());
        Assert.Equal(2, only.GetProperty("line").GetInt32());
        Assert.Equal("expected hold (outcome resolved), got done (outcome resolved)", only.GetProperty("detail").GetString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Scaffold_prints_a_case_that_then_passes()
    {
        await Put("placeholder\n");

        Assert.Equal(0, await Run("recipe", "eval", "--scaffold", Recipe));
        var scaffold = _out.ToString();
        Assert.StartsWith("{\"start\":{\"inputs\":{\"fact:run\":", scaffold, StringComparison.Ordinal);   // the recipe's one input, which it generates at a real start; the pure engine takes the scaffold's text

        await File.WriteAllTextAsync(Happy, scaffold, TestContext.Current.CancellationToken);
        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await Run("recipe", "eval", Recipe));
        Assert.Equal("happy pass\n", _out.ToString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Scaffold_out_writes_one_file_per_path_and_refuses_to_overwrite()
    {
        var route = Path.Combine(_home.Path, "recipes", "workflow.dev-evidence-route.yaml");
        var directory = Path.Combine(_home.Path, "scaffolds");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(route)!); // why: the recipe's directory
        await File.WriteAllTextAsync(route, ShippedRecipes.Read("dev/workflow.dev-evidence-route.yaml"), TestContext.Current.CancellationToken);
        string[] names = ["scaffold-ui", "scaffold-api", "scaffold-db", "scaffold-all", "scaffold-none", "scaffold-other", "scaffold-uncertain", "scaffold-step_failed"];

        Assert.Equal(0, await Run("recipe", "eval", "--scaffold", route, "--out", directory));

        Assert.Equal(names.Select(n => Path.Combine(directory, n + ".jsonl")), _out.ToString().TrimEnd('\n').Split('\n'), StringComparer.Ordinal);
        Assert.Equal(names.Order(StringComparer.Ordinal), Directory.GetFiles(directory).Select(f => Path.GetFileNameWithoutExtension(f)).Order(StringComparer.Ordinal));
        var first = await File.ReadAllTextAsync(Path.Combine(directory, "scaffold-ui.jsonl"), TestContext.Current.CancellationToken);
        Assert.Contains("\"scaffold\":true", first, StringComparison.Ordinal);

        await File.WriteAllTextAsync(Path.Combine(directory, "scaffold-api.jsonl"), "mine\n", TestContext.Current.CancellationToken);
        _out.GetStringBuilder().Clear();
        Assert.Equal(2, await Run("recipe", "eval", "--scaffold", route, "--out", directory));
        Assert.Contains("SND-CLI-003", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal("mine\n", await File.ReadAllTextAsync(Path.Combine(directory, "scaffold-api.jsonl"), TestContext.Current.CancellationToken));

        foreach (var name in names.Where(static n => !string.Equals(n, "scaffold-step_failed", StringComparison.Ordinal)))
        {
            File.Delete(Path.Combine(directory, name + ".jsonl"));
        }

        Assert.Equal(2, await Run("recipe", "eval", "--scaffold", route, "--out", directory));   // the last file still exists: nothing is written, not even the missing ones
        Assert.Equal(["scaffold-step_failed"], Directory.GetFiles(directory).Select(static f => Path.GetFileNameWithoutExtension(f)), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Scaffold_out_to_a_path_that_is_a_file_is_exit_2_with_a_fix_and_not_a_crash()
    {
        var route = Path.Combine(_home.Path, "recipes", "workflow.dev-evidence-route.yaml");
        var occupied = Path.Combine(_home.Path, "occupied");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(route)!); // why: the recipe's directory
        await File.WriteAllTextAsync(route, ShippedRecipes.Read("dev/workflow.dev-evidence-route.yaml"), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(occupied, "a file\n", TestContext.Current.CancellationToken);

        Assert.Equal(2, await Run("recipe", "eval", "--scaffold", route, "--out", occupied));

        Assert.Contains("SND-CLI-003", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("(fix: ", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal("a file\n", await File.ReadAllTextAsync(occupied, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Scaffold_out_that_fails_part_way_removes_the_files_it_wrote()
    {
        var route = Path.Combine(_home.Path, "recipes", "workflow.dev-evidence-route.yaml");
        var directory = Path.Combine(_home.Path, "scaffolds");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(route)!); // why: the recipe's directory
        await File.WriteAllTextAsync(route, ShippedRecipes.Read("dev/workflow.dev-evidence-route.yaml"), TestContext.Current.CancellationToken);
        _ = Directory.CreateDirectory(Path.Combine(directory, "scaffold-step_failed.jsonl"));   // the last case's file can't be written: a directory is there

        Assert.Equal(2, await Run("recipe", "eval", "--scaffold", route, "--out", directory));

        Assert.Contains("SND-CLI-003", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("can't write the scaffold to", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Record_of_an_instance_the_ledger_never_saw_exits_2_naming_it_and_writes_nothing()
    {
        Assert.Equal(2, await Run("recipe", "eval", "--record", "T-3/step/1", "--out", Happy));
        Assert.Contains("T-3/step/1", _error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("needs the server", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Happy));
    }

    private async Task Put(string happy)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(Happy)!); // why: the eval path has a parent
        await File.WriteAllTextAsync(Recipe, ShippedRecipes.Read("dev/workflow.dev-timeout.yaml"), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Happy, happy, TestContext.Current.CancellationToken);
    }

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, _ => null };

    private Task<int> Run(params string[] args) => CommandTable.Run(args, Environment, TimeProvider.System);
}
