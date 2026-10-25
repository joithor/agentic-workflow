using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Recipes;

namespace Aw.Sindri.Ui.Tests.Recipes;

/// <summary>The recipe sheet's reads and header (screens spec §4.3, U10a): every word a field of a recipe golden, and the two failure states.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipeSheetViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private const string WithName = "workflow.dev-triage-three-state";
    private const string Unnamed = "workflow.project";
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonNode Golden(string id) => GoldenFiles.Node("workflow-recipe", id);

    private static string Text(JsonNode? node) => (string)node!;

    private static string Hash12(string id) => Text(Golden(id)["hash"])["sha256:".Length..][..12];

    private async Task<RecipeSheetViewModel> Opened(string subject)
    {
        var sheet = new RecipeSheetViewModel(_server);
        await sheet.Open(_api, subject, Token);
        return sheet;
    }

    [Fact]
    public async Task Reads_the_approved_recipe_for_a_bare_id()
    {
        var sheet = await Opened(WithName);

        Assert.Equal((FramePhase.Shown, Text(Golden(WithName)["id"])), (sheet.Phase, sheet.Id));
        Assert.Equal((WithName, null), _server.LastRecipeRead);
    }

    [Fact]
    public async Task Reads_the_pinned_hash_when_the_link_names_one()
    {
        var sheet = await Opened(WithName + "@" + Hash12(WithName));

        Assert.Equal((FramePhase.Shown, WithName), (sheet.Phase, sheet.Id));
        Assert.Equal((WithName, Hash12(WithName)), _server.LastRecipeRead);
    }

    [Theory]
    [InlineData(WithName)]
    [InlineData(Unnamed)]
    [InlineData("workflow.fork-demo")]
    public async Task Header_reads_id_hash12_version_urgency(string id)
    {
        var sheet = await Opened(id);
        var golden = Golden(id);

        Assert.Equal($"{id}@{Hash12(id)} · v{golden["version"]!.GetValue<int>().ToString(CultureInfo.InvariantCulture)} · {Text(golden["urgency"])}", sheet.Identity);
    }

    [Fact]
    public async Task The_title_is_the_served_display_name_and_otherwise_the_id_made_readable()
    {
        var plain = await Opened(Unnamed);
        var body = (JsonObject)Golden(Unnamed);
        body["displayName"] = "Plan and track a project";
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
        var named = await Opened(Unnamed);

        Assert.Equal(("Project", "Plan and track a project"), (plain.Title, named.Title));
        Assert.DoesNotContain("workflow.", named.Title, StringComparison.Ordinal);
        Assert.Contains(Unnamed, named.Identity, StringComparison.Ordinal);   // the id stays, in Details
    }

    [Fact]
    public async Task Details_start_closed_and_the_choice_is_kept_while_the_sheet_is_read_again()
    {
        var sheet = await Opened(Unnamed);
        Assert.False(sheet.IsDetailsOpen);

        sheet.ToggleDetails();
        await sheet.Refresh(_api, Token);

        Assert.True(sheet.IsDetailsOpen);
    }

    [Fact]
    public async Task Name_template_shows_only_when_served()
    {
        var named = await Opened(WithName);
        var unnamed = await Opened(Unnamed);

        Assert.Equal((Text(Golden(WithName)["name"]), true), (named.Name, named.HasName));
        Assert.Equal((string.Empty, false), (unnamed.Name, unnamed.HasName));
    }

    [Fact]
    public async Task Not_found_shows_the_served_detail_with_Back()
    {
        var sheet = await Opened("workflow.unknown");

        Assert.Equal((FramePhase.NotFound, true, Labels.Back), (sheet.Phase, sheet.IsProblem, sheet.BackText));
        Assert.Contains(Text(GoldenFiles.Node("workflow-recipe", "not-found")["detail"]), sheet.ProblemText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unreadable_shows_the_shared_state_with_Back()
    {
        var body = (JsonObject)Golden(WithName);
        _ = body.Remove("states");
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());

        var sheet = await Opened(WithName);

        Assert.Equal((FramePhase.Unreadable, Labels.RecipeUnreadable, Labels.Back), (sheet.Phase, sheet.ProblemText, sheet.BackText));
        Assert.DoesNotContain("P11", sheet.ProblemText, StringComparison.Ordinal);   // the workflow list's sentence names the wrong thing and an API phase that has landed
        Assert.DoesNotContain("workflow list", sheet.ProblemText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_first_read_shows_the_served_code_and_detail_with_Back()
    {
        _server.WorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-API-010", "views are rebuilding");

        var sheet = await Opened(WithName);

        Assert.Equal((FramePhase.Refused, true), (sheet.Phase, sheet.IsProblem));
        Assert.Contains("SND-API-010", sheet.ProblemText, StringComparison.Ordinal);
        Assert.Contains("views are rebuilding", sheet.ProblemText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_reread_keeps_what_was_shown()
    {
        var sheet = await Opened(WithName);
        var shown = sheet.Identity;
        _server.WorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-503", "busy");

        await sheet.Refresh(_api, Token);

        Assert.Equal((FramePhase.Shown, shown, false), (sheet.Phase, sheet.Identity, sheet.IsProblem));
    }

    [Fact]
    public async Task A_reread_that_no_longer_finds_the_recipe_keeps_it_and_says_so()
    {
        var sheet = await Opened(WithName);
        _server.WorkflowAnswer = RouteAnswer.Problem("workflow-recipe", "not-found");

        await sheet.Refresh(_api, Token);

        Assert.Equal((FramePhase.Shown, Labels.RecipeGone, true), (sheet.Phase, sheet.Note, sheet.HasNote));
    }
}
