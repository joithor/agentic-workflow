using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Web;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's workflow reads (API §3.8): the routes, the query, the id-only addressing (Q-API-2) and what comes back, over the API's goldens.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientWorkflowTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SindriApiClient Client(Canned canned) => new(new HttpClient(canned), new InMemoryHumanToken("tok-1"), TimeProvider.System);

    private static Canned Serving(string slug, string scenario) => new(HttpStatusCode.OK, GoldenFiles.Api(slug, scenario));

    private static JsonNode Golden(string slug, string scenario) => JsonNode.Parse(GoldenFiles.Api(slug, scenario))!; // why: the golden is a JSON document

    [Fact]
    public async Task Instances_sends_root_open_recipe_and_subject_as_query_parameters()
    {
        var served = Golden("workflow-instances", "recipe-filter")["instances"]![0]!; // why: the golden has rows
        var recipe = (string)served["recipe"]!["id"]!; // why: every row names its recipe
        var subject = (string)served["subject"]!["label"]!; // why: and its subject
        var canned = Serving("workflow-instances", "recipe-filter");

        var list = await Client(canned).Instances(_api, new InstanceQuery(Root: true, Open: true, Recipe: recipe, Subject: subject), Token);

        Assert.True(list.IsSuccess);
        Assert.NotEmpty(list.Value.Instances);
        var seen = Assert.Single(canned.Seen);
        Assert.Equal("/sindri/workflow/instances", seen.Uri.AbsolutePath);
        var query = HttpUtility.ParseQueryString(seen.Uri.Query);
        Assert.Equal(["true", "true", recipe, subject], [query["root"], query["open"], query["recipe"], query["subject"]]);
        Assert.Null(query["address"]);
    }

    [Fact]
    public async Task Instances_sends_only_the_members_that_narrow()
    {
        var canned = Serving("workflow-instances", "root");

        _ = await Client(canned).Instances(_api, new InstanceQuery(Root: true), Token);

        Assert.Equal("?root=true", Assert.Single(canned.Seen).Uri.Query);
    }

    [Fact]
    public async Task An_address_goes_in_as_a_query_parameter_never_a_path_segment()
    {
        var address = (string)Golden("workflow-instance", "project-build")["address"]!; // why: every instance has one
        var canned = Serving("workflow-instances", "open");

        _ = await Client(canned).Instances(_api, new InstanceQuery(Address: address), Token);

        var seen = Assert.Single(canned.Seen).Uri;
        Assert.Equal("/sindri/workflow/instances", seen.AbsolutePath);
        Assert.Equal(address, HttpUtility.ParseQueryString(seen.Query)["address"]);
        Assert.Contains("%2F", seen.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Instance_sends_the_opaque_id_as_the_only_path_segment_and_reads_the_view()
    {
        var golden = Golden("workflow-instance", "project-build");
        var id = (string)golden["id"]!; // why: every instance has one
        var canned = Serving("workflow-instance", "project-build");

        var instance = await Client(canned).Instance(_api, id, Token);

        Assert.True(instance.IsSuccess);
        Assert.Equal(((string)golden["address"]!, id), (instance.Value.Address, instance.Value.Id));
        var seen = Assert.Single(canned.Seen).Uri;
        Assert.Equal($"/sindri/workflow/instances/{id}", seen.AbsolutePath);
        Assert.Equal(string.Empty, seen.Query);
    }

    [Fact]
    public async Task Instance_maps_SND_ITEM_404_with_its_detail()
    {
        var golden = Golden("workflow-instance", "not-found");
        var canned = new Canned(HttpStatusCode.NotFound, GoldenFiles.Api("workflow-instance", "not-found"));

        var instance = await Client(canned).Instance(_api, "WFI-unknown", Token);

        Assert.Equal(("SND-ITEM-404", ErrorKind.NotFound, (string)golden["detail"]!), (instance.Error.Code, instance.Error.Kind, instance.Error.Message)); // why: a refusal has a detail
    }

    [Fact]
    public async Task InstanceFact_reads_a_truncated_body_and_says_so()
    {
        var golden = Golden("workflow-instance-fact", "truncated");
        var key = (string)golden["key"]!; // why: every fact has one
        var canned = Serving("workflow-instance-fact", "truncated");

        var fact = await Client(canned).InstanceFact(_api, "WFI-1", key, Token);

        Assert.True(fact.IsSuccess);
        Assert.Equal((key, true), (fact.Value.Key, fact.Value.Truncated));
        Assert.Equal(FactType.Payload, fact.Value.Type);
        Assert.True(JsonNode.DeepEquals(golden["body"], JsonNode.Parse(fact.Value.Body.GetRawText())));
        Assert.Equal($"/sindri/workflow/instances/WFI-1/facts/{key}", Uri.UnescapeDataString(Assert.Single(canned.Seen).Uri.AbsolutePath));
    }

    [Fact]
    public async Task InstanceFact_maps_SND_ITEM_404()
    {
        var golden = Golden("workflow-instance-fact", "not-found");
        var canned = new Canned(HttpStatusCode.NotFound, GoldenFiles.Api("workflow-instance-fact", "not-found"));

        var fact = await Client(canned).InstanceFact(_api, "WFI-1", "fact:unknown", Token);

        Assert.Equal(("SND-ITEM-404", ErrorKind.NotFound, (string)golden["detail"]!), (fact.Error.Code, fact.Error.Kind, fact.Error.Message)); // why: a refusal has a detail
    }

    [Fact]
    public async Task Recipe_reads_inputs_type_kind_and_outputs_from()
    {
        var golden = Golden("workflow-recipe", "workflow-project");
        var instance = Golden("workflow-instance", "project-build")["recipe"]!; // why: the instance names the recipe it runs
        var (id, hash) = ((string)instance["id"]!, (string)instance["hash"]!); // why: both are always served
        var canned = Serving("workflow-recipe", "workflow-project");

        var recipe = await Client(canned).Recipe(_api, id, hash, Token);

        Assert.True(recipe.IsSuccess);
        var first = golden["inputs"]![0]!; // why: the recipe declares inputs
        Assert.Equal(((string)first["key"]!, (string?)first["kind"], (bool)first["required"]!), (recipe.Value.Inputs[0].Key, recipe.Value.Inputs[0].Kind, recipe.Value.Inputs[0].Required));
        Assert.Equal(FactType.Payload, recipe.Value.Inputs[0].Type);
        Assert.Equal(golden["outputs"]!.AsArray().Select(o => (string)o!["from"]!), recipe.Value.Outputs.Select(o => o.From), StringComparer.Ordinal); // why: every output has one
        Assert.Equal($"/sindri/workflow/recipes/{id}@{hash}", Uri.UnescapeDataString(Assert.Single(canned.Seen).Uri.AbsolutePath));
    }

    [Fact]
    public async Task A_recipe_without_a_hash_is_asked_for_by_its_id()
    {
        var canned = Serving("workflow-recipe", "workflow-project");

        _ = await Client(canned).Recipe(_api, "workflow.project", hash: null, Token);

        Assert.Equal("/sindri/workflow/recipes/workflow.project", Assert.Single(canned.Seen).Uri.AbsolutePath);
    }

    // Every workflow read is a human's from the UI, like every call: never X-Sindri-Terminal, never Origin.
    [Fact]
    public async Task Workflow_calls_send_human_ui_and_never_terminal_or_origin()
    {
        var canned = Serving("workflow-instances", "root");
        var client = Client(canned);

        _ = await client.Instances(_api, new InstanceQuery(Root: true), Token);
        _ = await client.Instance(_api, "WFI-1", Token);
        _ = await client.InstanceFact(_api, "WFI-1", "fact:a", Token);
        _ = await client.Recipe(_api, "workflow.project", hash: null, Token);

        Assert.Equal(4, canned.Seen.Count);
        Assert.All(canned.Seen, r => Assert.Equal(("human", "ui", "tok-1", null, null), (r.Caller, r.Client, r.Token, r.Terminal, r.Origin)));
        Assert.All(canned.Seen, r => Assert.StartsWith("/sindri/workflow/", r.Uri.AbsolutePath, StringComparison.Ordinal));
    }

    private sealed record Seen(Uri Uri, string? Caller, string? Client, string? Token, string? Terminal, string? Origin);

    private sealed class Canned(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public List<Seen> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? Header(string name) => request.Headers.TryGetValues(name, out var values) ? values.Single() : null;
            Seen.Add(new Seen(request.RequestUri!, Header("X-Sindri-Caller"), Header("X-Sindri-Client"), Header("X-Sindri-Token"), Header("X-Sindri-Terminal"), Header("Origin"))); // why: HttpClient always sets the URI
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
