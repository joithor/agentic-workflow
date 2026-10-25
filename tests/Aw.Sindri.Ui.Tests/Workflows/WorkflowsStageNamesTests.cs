using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The Workflows page says a run's stage in its recipe's words once the recipe is read, and in the id made readable until then or when it cannot be read.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsStageNamesTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private int _recipeReads;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The golden recipe of a workflow with one state given a displayName.
    private static RouteAnswer Named(string recipeId, string state, string name)
    {
        var recipe = JsonNode.Parse(GoldenFiles.Api("workflow-recipe", recipeId))!;
        recipe["states"]!.AsArray().Single(s => (string?)s!["id"] == state)!["displayName"] = name;
        return new RouteAnswer(HttpStatusCode.OK, recipe.ToJsonString());
    }

    private async Task<WorkflowsViewModel> Shown()
    {
        var list = new WorkflowsViewModel(_server, new FakeClipboard(), new FakeTimeProvider(A.Noon));
        list.SetViewMode(WorkflowViewMode.List);
        await list.Refresh(_api, Token);
        return list;
    }

    private static InstanceRowViewModel Triage(WorkflowsViewModel list) =>
        list.Groups.SelectMany(g => g.Rows).Single(r => string.Equals(r.Address, "ITM-01JK2/triage-alert/1", StringComparison.Ordinal));

    [Fact]
    public async Task A_row_says_its_stage_in_the_recipes_words_once_the_recipe_is_read()
    {
        _server.RecipeBody = id => { _recipeReads++; return string.Equals(id, "workflow.triage-alert", StringComparison.Ordinal) ? Named(id, "classify", "Classify the alert") : null; };

        var row = Triage(await Shown());

        Assert.Equal("Classify the alert", row.StageName);
        Assert.EndsWith("Classify the alert", row.Compact, StringComparison.Ordinal);
        Assert.Contains("Classify the alert", row.AccessibleName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_recipe_that_cannot_be_read_leaves_the_id_made_readable()
    {
        _server.RecipeBody = _ => new RouteAnswer(HttpStatusCode.NotFound, "{}");

        var row = Triage(await Shown());

        Assert.Equal("Classify", row.StageName);
    }

    [Fact]
    public async Task Each_recipe_on_the_page_is_read_once_however_often_the_list_is_read()
    {
        _server.RecipeBody = id => { _recipeReads++; return null; };
        var list = await Shown();

        await list.Refresh(_api, Token);
        await list.Refresh(_api, Token);

        var recipes = list.Groups.SelectMany(g => g.Rows).Select(r => r.Recipe).Distinct(StringComparer.Ordinal).Count();
        Assert.Equal(recipes, _recipeReads);
    }
}
