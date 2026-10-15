using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The contract (<see cref="SindriApiContract"/>, continued): the workflow reads (API §3.8) as the goldens serve them.
/// Every scenario is found by enumerating the goldens on disk, never listed, so a golden the API lane adds or renames is
/// read the moment it lands.
/// </summary>
public abstract partial class SindriApiContract
{
    /// <summary>Every served instance golden.</summary>
    public static TheoryData<string> InstanceScenarios => Scenarios("workflow-instance");

    /// <summary>Every served fact golden.</summary>
    public static TheoryData<string> FactScenarios => Scenarios("workflow-instance-fact");

    /// <summary>Every served recipe golden whose scenario is a recipe id (the recipe's dots are dashes).</summary>
    public static TheoryData<string> RecipeScenarios => Scenarios("workflow-recipe");

    private static TheoryData<string> Scenarios(string slug) =>
        [.. GoldenFiles.All().Where(g => g.Kind == "Api" && g.Slug == slug && !IsRefusal(g.Body)).Select(g => g.Scenario)];

    private static bool IsRefusal(string body) => JsonNode.Parse(body) is JsonObject json && json["traceId"] is not null;

    private static JsonNode Golden(string slug, string scenario) => JsonNode.Parse(GoldenFiles.Api(slug, scenario))!; // why: the golden is a JSON document

    private static string[] Ids(JsonNode list) => [.. list["instances"]!.AsArray().Select(i => (string)i!["id"]!)]; // why: every row has an id

    [Fact]
    public async Task Instances_answer_every_root_row_and_the_root_wide_counts()
    {
        var golden = Golden("workflow-instances", "root");

        var list = (await Reach(new FakeSindriApi()).Instances(_api, new InstanceQuery(), Token)).Value;

        Expect.Sequence(Ids(golden), list.Instances.Select(i => i.Id));
        Expect.Equal(((int)golden["counts"]!["open"]!, (int)golden["counts"]!["needsYou"]!, (int)golden["counts"]!["done"]!), (list.Counts.Open, list.Counts.NeedsYou, list.Counts.Done));
    }

    [Fact]
    public async Task Instances_open_keeps_the_rows_that_are_not_terminal_and_the_counts_stay_root_wide()
    {
        var golden = Golden("workflow-instances", "open");

        var list = (await Reach(new FakeSindriApi()).Instances(_api, new InstanceQuery(Root: true, Open: true), Token)).Value;

        Expect.Sequence(Ids(golden), list.Instances.Select(i => i.Id));
        Expect.Equal(list.Counts.Open, list.Instances.Count);
        Expect.True(list.Counts.Done > 0);
    }

    [Fact]
    public async Task Instances_recipe_keeps_that_recipe_in_the_order_of_the_golden()
    {
        var golden = Golden("workflow-instances", "recipe-filter");
        var recipe = (string)golden["instances"]![0]!["recipe"]!["id"]!; // why: every row names its recipe

        var list = (await Reach(new FakeSindriApi()).Instances(_api, new InstanceQuery(Recipe: recipe), Token)).Value;

        Expect.Sequence(Ids(golden), list.Instances.Select(i => i.Id));
        Expect.True(list.Instances.All(i => i.Recipe.Id == recipe));
    }

    [Fact]
    public async Task Instances_subject_keeps_the_rows_of_that_subject_by_label_or_key()
    {
        var row = Golden("workflow-instances", "root")["instances"]![0]!; // why: the golden has rows
        var label = (string)row["subject"]!["label"]!; // why: every subject has one
        var key = (string)row["subject"]!["key"]!; // why: and a key
        var api = Reach(new FakeSindriApi());

        var byLabel = (await api.Instances(_api, new InstanceQuery(Subject: label), Token)).Value;
        var byKey = (await api.Instances(_api, new InstanceQuery(Subject: key), Token)).Value;

        Expect.True(byLabel.Instances.Count > 0);
        Expect.True(byLabel.Instances.All(i => i.Subject.Label == label));
        Expect.True(byKey.Instances.All(i => i.Subject.Key == key));
        Expect.True(byLabel.Instances.All(l => byKey.Instances.Any(k => k.Id == l.Id)));
    }

    [Fact]
    public async Task Instances_address_finds_the_one_row_to_read_its_opaque_id()
    {
        var row = Golden("workflow-instances", "root")["instances"]![0]!; // why: the golden has rows
        var api = Reach(new FakeSindriApi());

        var found = (await api.Instances(_api, new InstanceQuery(Address: (string)row["address"]!), Token)).Value; // why: every row has one
        var none = (await api.Instances(_api, new InstanceQuery(Address: "ITM-none/none/1"), Token)).Value;

        Expect.Sequence([(string)row["id"]!], found.Instances.Select(i => i.Id)); // why: every row has an id
        Expect.Equal(0, none.Instances.Count);
    }

    [Theory]
    [MemberData(nameof(InstanceScenarios))]
    public async Task Instance_answers_each_golden_by_its_opaque_id(string scenario)
    {
        var golden = Golden("workflow-instance", scenario);

        var instance = (await Reach(new FakeSindriApi()).Instance(_api, (string)golden["id"]!, Token)).Value; // why: every instance has an id

        Expect.Equal((string)golden["address"]!, instance.Address); // why: and an address
        Expect.Equal((string)golden["state"]!, instance.State); // why: and a state
    }

    [Fact]
    public async Task Instance_that_does_not_exist_is_the_goldens_not_found_refusal()
    {
        var golden = Golden("workflow-instance", "not-found");

        var refused = await Reach(new FakeSindriApi()).Instance(_api, "WFI-unknown", Token);

        Expect.Equal(new Error("SND-ITEM-404", ErrorKind.NotFound, (string)golden["detail"]!), refused.Error); // why: a refusal has a detail
    }

    [Theory]
    [MemberData(nameof(FactScenarios))]
    public async Task InstanceFact_answers_each_golden_by_its_key(string scenario)
    {
        var golden = Golden("workflow-instance-fact", scenario);
        var id = (string)Golden("workflow-instances", "root")["instances"]![0]!["id"]!; // why: the golden has rows

        var fact = (await Reach(new FakeSindriApi()).InstanceFact(_api, id, (string)golden["key"]!, Token)).Value; // why: every fact has a key

        Expect.Equal((string)golden["key"]!, fact.Key);
        Expect.Equal((bool)golden["truncated"]!, fact.Truncated); // why: and says whether it is cut
    }

    [Fact]
    public async Task InstanceFact_that_is_not_declared_is_the_goldens_not_found_refusal()
    {
        var golden = Golden("workflow-instance-fact", "not-found");
        var id = (string)Golden("workflow-instances", "root")["instances"]![0]!["id"]!; // why: the golden has rows
        var api = Reach(new FakeSindriApi());

        var unknownKey = await api.InstanceFact(_api, id, "fact:unknown", Token);
        var unknownInstance = await api.InstanceFact(_api, "WFI-unknown", "fact:unknown", Token);

        Expect.Equal(new Error("SND-ITEM-404", ErrorKind.NotFound, (string)golden["detail"]!), unknownKey.Error); // why: a refusal has a detail
        Expect.Equal("SND-ITEM-404", unknownInstance.Error.Code);
    }

    [Theory]
    [MemberData(nameof(RecipeScenarios))]
    public async Task Recipe_answers_each_golden_by_its_recipe_id(string scenario)
    {
        var golden = Golden("workflow-recipe", scenario);

        var recipe = (await Reach(new FakeSindriApi()).Recipe(_api, scenario.Replace('-', '.'), hash: null, Token)).Value;

        Expect.Equal(golden["inputs"]!.AsArray().Count, recipe.Inputs.Count);
        Expect.Equal(golden["outputs"]!.AsArray().Count, recipe.Outputs.Count);
    }

    [Fact]
    public async Task Recipe_that_does_not_exist_is_SND_ITEM_404()
    {
        var refused = await Reach(new FakeSindriApi()).Recipe(_api, "workflow.none", hash: null, Token);

        Expect.Equal(("SND-ITEM-404", ErrorKind.NotFound), (refused.Error.Code, refused.Error.Kind));
        Expect.True(refused.Error.Message.Length > 0);
    }

    // F12 (test-0), as for the other routes: a refusal, a stopped server and an odd body reach every workflow route the same way.
    [Fact]
    public async Task A_403_refusal_reaches_every_workflow_route_as_sindris_code_and_detail()
    {
        var api = Reach(new FakeSindriApi { Refusal = A.NoTokenRefusal });
        var expected = new Error(A.NoTokenRefusal.Code, ErrorKind.Forbidden, A.NoTokenRefusal.Detail);

        Expect.Equal(expected, (await api.Instances(_api, new InstanceQuery(), Token)).Error);
        Expect.Equal(expected, (await api.Instance(_api, "WFI-1", Token)).Error);
        Expect.Equal(expected, (await api.InstanceFact(_api, "WFI-1", "fact:a", Token)).Error);
        Expect.Equal(expected, (await api.Recipe(_api, "workflow.project", hash: null, Token)).Error);
    }

    [Fact]
    public async Task A_stopped_server_is_unreachable_on_every_workflow_route()
    {
        var server = new FakeSindriApi();
        server.GoDown();
        var api = Reach(server);

        Expect.Equal("SND-UI-002", (await api.Instances(_api, new InstanceQuery(), Token)).Error.Code);
        Expect.Equal("SND-UI-002", (await api.Instance(_api, "WFI-1", Token)).Error.Code);
        Expect.Equal("SND-UI-002", (await api.InstanceFact(_api, "WFI-1", "fact:a", Token)).Error.Code);
        Expect.Equal("SND-UI-002", (await api.Recipe(_api, "workflow.project", hash: null, Token)).Error.Code);
    }

    [Theory]
    [InlineData(409, ErrorKind.Conflict)]
    [InlineData(429, ErrorKind.RateLimited)]
    [InlineData(504, ErrorKind.Timeout)]
    [InlineData(503, ErrorKind.Unavailable)]
    [InlineData(404, ErrorKind.NotFound)]
    public async Task A_refusal_keeps_its_code_detail_and_kind_on_every_workflow_route(int status, ErrorKind kind)
    {
        var api = Reach(new FakeSindriApi { WorkflowAnswer = RouteAnswer.Problem((HttpStatusCode)status, "SND-API-777", "Not now (fix: later)") });
        var expected = new Error("SND-API-777", kind, "Not now (fix: later)");

        Expect.Equal(expected, (await api.Instances(_api, new InstanceQuery(), Token)).Error);
        Expect.Equal(expected, (await api.Instance(_api, "WFI-1", Token)).Error);
        Expect.Equal(expected, (await api.InstanceFact(_api, "WFI-1", "fact:a", Token)).Error);
        Expect.Equal(expected, (await api.Recipe(_api, "workflow.project", hash: null, Token)).Error);
    }

    [Theory]
    [InlineData(500, "boom")]
    [InlineData(418, "{\"title\":\"Teapot\",\"detail\":\"short and stout\"}")]
    [InlineData(500, "{\"title\":\"SND-API-001\"}")]
    public async Task A_body_that_is_not_sindris_is_an_unreadable_answer_on_every_workflow_route(int status, string body)
    {
        var api = Reach(new FakeSindriApi { WorkflowAnswer = new RouteAnswer((HttpStatusCode)status, body) });

        Expect.Equal("SND-UI-003", (await api.Instances(_api, new InstanceQuery(), Token)).Error.Code);
        Expect.Equal("SND-UI-003", (await api.Instance(_api, "WFI-1", Token)).Error.Code);
        Expect.Equal("SND-UI-003", (await api.InstanceFact(_api, "WFI-1", "fact:a", Token)).Error.Code);
        Expect.Equal("SND-UI-003", (await api.Recipe(_api, "workflow.project", hash: null, Token)).Error.Code);
    }
}
