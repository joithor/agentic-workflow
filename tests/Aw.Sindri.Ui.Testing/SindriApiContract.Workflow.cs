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

    /// <summary>Every served recipe golden.</summary>
    public static TheoryData<string> RecipeScenarios => Scenarios("workflow-recipe");

    /// <summary>Every distinct recipe id an instance golden or a list golden's row runs: each is a link the app can follow to the recipe.</summary>
    public static TheoryData<string> RunningRecipeIds => [.. RunningRecipes().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    private static IEnumerable<string> RunningRecipes() =>
        from g in GoldenFiles.All()
        where g.Kind == "Api" && (string.Equals(g.Slug, "workflow-instance", StringComparison.Ordinal) || string.Equals(g.Slug, "workflow-instances", StringComparison.Ordinal)) && !IsRefusal(g.Body)
        let json = JsonNode.Parse(g.Body)!
        from named in json["instances"] is JsonArray rows ? rows.Select(r => r!["recipe"]!) : [json["recipe"]!] // why: a list's rows and an instance both name their recipe
        select (string)named["id"]!; // why: a recipe has an id

    /// <summary>Every fact golden with each instance golden it is served for (the fake's table).</summary>
    public static TheoryData<string, string> BoundFacts =>
        [.. WorkflowAnswers.FactGoldens.SelectMany(g => g.Value.Select(instance => (g.Key, instance)))];

    private static TheoryData<string> Scenarios(string slug) =>
        [.. GoldenFiles.All().Where(g => g.Kind == "Api" && g.Slug == slug && !IsRefusal(g.Body)).Select(g => g.Scenario)];

    private static bool IsRefusal(string body) => JsonNode.Parse(body) is JsonObject json && json["traceId"] is not null;

    private static JsonNode Golden(string slug, string scenario) => JsonNode.Parse(GoldenFiles.Api(slug, scenario))!; // why: the golden is a JSON document

    private static string[] Ids(JsonNode list) => [.. list["instances"]!.AsArray().Select(i => (string)i!["id"]!)]; // why: every row has an id

    [Fact]
    public async Task Instances_answer_every_root_row_and_the_root_wide_counts()
    {
        var golden = Golden("workflow-instances", "root");

        var list = (await Reach(new FakeSindriApi()).Instances(_api, new InstanceQuery(Root: true), Token)).Value;

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

    // A fact body belongs to one instance: where an instance's fact row counts what the body holds ("24 tasks", "6 of 24 packs"),
    // the fake serves a body that agrees with the row or refuses, never another instance's body under the same key.
    [Fact]
    public async Task InstanceFact_never_serves_a_body_that_contradicts_its_instance_s_fact_row()
    {
        var api = Reach(new FakeSindriApi());
        var checkedRows = 0;
        foreach (var instance in GoldenFiles.All().Where(g => g.Kind == "Api" && g.Slug == "workflow-instance" && !IsRefusal(g.Body)).Select(g => JsonNode.Parse(g.Body)!))
        {
            foreach (var row in instance["facts"]!.AsArray().Select(f => f!)) // why: an instance lists its facts
            {
                var key = (string)row["key"]!; // why: every row has a key
                var summary = (string?)row["summary"] ?? string.Empty;
                var served = await api.InstanceFact(_api, (string)instance["id"]!, key, Token); // why: every instance has an id
                if (served.IsError || (key is not ("fact:packet" or "fact:evidence")))
                {
                    continue;
                }

                checkedRows++;
                var body = JsonNode.Parse(served.Value.Body.GetRawText())!;
                var counted = key == "fact:packet"
                    ? Count(body["workstreams"]!.AsArray().Sum(w => w!["tasks"]!.AsArray().Count)) + " tasks" // why: a packet has workstreams of tasks
                    : Count((int)body["done"]!) + " of " + Count((int)body["total"]!) + " packs"; // why: an evidence fact counts its packs
                Expect.True(summary.Contains(counted, StringComparison.Ordinal));
            }
        }

        Expect.True(checkedRows > 0);
    }

    [Theory]
    [MemberData(nameof(BoundFacts))]
    public async Task InstanceFact_serves_the_golden_of_its_scenario_for_the_instance_it_is_bound_to(string scenario, string instanceScenario)
    {
        var golden = Golden("workflow-instance-fact", scenario);
        var key = (string)golden["key"]!; // why: every fact has a key
        var id = (string)Golden("workflow-instance", instanceScenario)["id"]!; // why: every instance has an id

        var fact = (await Reach(new FakeSindriApi()).InstanceFact(_api, id, key, Token)).Value;

        Expect.Equal(golden["body"]!.ToJsonString(), JsonNode.Parse(fact.Body.GetRawText())!.ToJsonString()); // why: every fact has a body
        Expect.Equal((bool)golden["truncated"]!, fact.Truncated);
    }

    [Fact]
    public async Task InstanceFact_of_an_instance_that_does_not_list_the_key_is_not_found()
    {
        var golden = Golden("workflow-instance-fact", "not-found");
        var listed = InstanceListing("fact:scope");
        var other = Golden("workflow-instances", "root")["instances"]!.AsArray().Select(r => (string)r!["id"]!)
            .First(id => id != listed && !InstanceFacts(id).Contains("fact:scope", StringComparer.Ordinal)); // why: rows have ids
        var api = Reach(new FakeSindriApi());

        var refused = await api.InstanceFact(_api, other, "fact:scope", Token);

        Expect.Equal(new Error("SND-ITEM-404", ErrorKind.NotFound, (string)golden["detail"]!), refused.Error); // why: a refusal has a detail
    }

    private static string Count(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string InstanceListing(string key) =>
        (string)GoldenFiles.All().Where(g => g.Kind == "Api" && g.Slug == "workflow-instance" && !IsRefusal(g.Body)).Select(g => JsonNode.Parse(g.Body)!)
            .First(i => i["facts"]!.AsArray().Any(f => (string?)f!["key"] == key))["id"]!; // why: every instance has an id

    private static string[] InstanceFacts(string id) =>
        [.. GoldenFiles.All().Where(g => g.Kind == "Api" && g.Slug == "workflow-instance" && !IsRefusal(g.Body)).Select(g => JsonNode.Parse(g.Body)!)
            .Where(i => (string?)i["id"] == id).SelectMany(i => i["facts"]!.AsArray().Select(f => (string)f!["key"]!))]; // why: facts have keys

    [Theory]
    [MemberData(nameof(RecipeScenarios))]
    public async Task Recipe_answers_each_golden_for_the_recipe_it_is_named_for(string scenario)
    {
        var golden = Golden("workflow-recipe", scenario);
        var id = WorkflowAnswers.NamedRecipeGoldens[scenario];

        var recipe = (await Reach(new FakeSindriApi()).Recipe(_api, id, hash: null, Token)).Value;

        Expect.Equal(golden["inputs"]!.AsArray().Count, recipe.Inputs.Count);
        Expect.Equal(golden["outputs"]!.AsArray().Count, recipe.Outputs.Count);
    }

    [Theory]
    [MemberData(nameof(RunningRecipeIds))]
    public async Task Every_recipe_an_instance_runs_answers_with_its_golden_when_it_has_one_and_is_not_found_when_not(string recipeId)
    {
        var golden = WorkflowAnswers.NamedRecipeGoldens.FirstOrDefault(g => string.Equals(g.Value, recipeId, StringComparison.Ordinal)).Key;

        var recipe = await Reach(new FakeSindriApi()).Recipe(_api, recipeId, hash: null, Token);

        if (golden is null)
        {
            Expect.Equal(("SND-ITEM-404", ErrorKind.NotFound), (recipe.Error.Code, recipe.Error.Kind));
        }
        else
        {
            Expect.Equal(Golden("workflow-recipe", golden)["inputs"]!.AsArray().Count, recipe.Value.Inputs.Count);
        }
    }

    [Fact]
    public async Task Recipe_that_does_not_exist_is_SND_ITEM_404()
    {
        var refused = await Reach(new FakeSindriApi()).Recipe(_api, "workflow.none", hash: null, Token);

        Expect.Equal(("SND-ITEM-404", ErrorKind.NotFound), (refused.Error.Code, refused.Error.Kind));
        Expect.True(refused.Error.Message.Length > 0);
    }

    [Fact]
    public async Task Recipe_with_the_hash_an_instance_runs_answers_and_any_other_hash_is_not_found()
    {
        var running = Golden("workflow-instance", "project-build")["recipe"]!; // why: the instance names the recipe it runs
        var (id, hash) = ((string)running["id"]!, (string)running["hash"]!); // why: both are always served
        var api = Reach(new FakeSindriApi());

        var named = await api.Recipe(_api, id, hash, Token);
        var wrong = await api.Recipe(_api, id, "sha256:" + new string('0', 64), Token);

        Expect.True(named.IsSuccess);
        Expect.Equal(("SND-ITEM-404", ErrorKind.NotFound), (wrong.Error.Code, wrong.Error.Kind));
    }

    [Fact]
    public async Task Instances_root_keeps_top_level_rows_and_without_it_the_child_rows_join()
    {
        var roots = Ids(Golden("workflow-instances", "root"));
        var child = (string)Golden("workflow-instance", "item-produce")["id"]!; // why: the golden is a child instance, with an id
        var api = Reach(new FakeSindriApi());

        var onlyRoots = (await api.Instances(_api, new InstanceQuery(Root: true), Token)).Value;
        var all = (await api.Instances(_api, new InstanceQuery(), Token)).Value;

        Expect.Sequence(roots, onlyRoots.Instances.Select(i => i.Id));
        Expect.True(all.Instances.Select(i => i.Id).Contains(child, StringComparer.Ordinal));
        Expect.True(roots.All(r => all.Instances.Any(i => i.Id == r)));
        Expect.Equal(onlyRoots.Counts, all.Counts);   // the counts stay root-wide
    }

    // The address filter finds the one instance by its address, its item id or its opaque id (the CLI's forms, API §3.8).
    [Fact]
    public async Task Instances_address_resolves_an_address_an_item_id_or_an_id()
    {
        var row = Golden("workflow-instances", "root")["instances"]![0]!; // why: the golden has rows
        var api = Reach(new FakeSindriApi());

        foreach (var form in new[] { (string)row["address"]!, (string)row["itemId"]!, (string)row["id"]! }) // why: a root row has all three
        {
            var found = (await api.Instances(_api, new InstanceQuery(Address: form), Token)).Value;

            Expect.Equal(1, found.Instances.Count);
            Expect.Equal((string)row["id"]!, found.Instances[0].Id);
        }
    }

    // An empty filter narrows nothing, as an absent one does (a client that sends "recipe=" asks for all).
    [Fact]
    public async Task Instances_with_empty_filters_are_not_narrowed()
    {
        var api = Reach(new FakeSindriApi());

        var bare = (await api.Instances(_api, new InstanceQuery(), Token)).Value;
        var empty = (await api.Instances(_api, new InstanceQuery(Recipe: "", Subject: "", Address: ""), Token)).Value;

        Expect.Sequence(bare.Instances.Select(i => i.Id), empty.Instances.Select(i => i.Id));
        Expect.True(empty.Instances.Count > 0);
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

    // LANE ANCHOR U3': nothing: U3' cases go in their own partial file. Insert immediately above this marker, never between the markers.
    // LANE ANCHOR U8: the send cases (nonce, commands, complete), once the goldens exist; enumerate them from disk like the reads. Insert immediately below this marker, never between the markers.
}
