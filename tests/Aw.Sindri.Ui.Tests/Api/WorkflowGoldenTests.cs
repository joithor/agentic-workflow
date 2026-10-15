using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>
/// The rev-8 workflow goldens (API spec §3.8, served by #213) through the app's mirror records. The files are read from
/// disk and enumerated, never listed, so a golden added or renamed by the API lane is read the moment it lands.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowGoldenTests
{
    private static readonly string[] _workflowSlugs = ["workflow-instances", "workflow-instance", "workflow-instance-fact", "workflow-status", "workflow-recipe"];

    // What each slug is written back as after the app read it.
    private static readonly Dictionary<string, Func<byte[], Result<byte[]>>> _roundTrip = new(StringComparer.Ordinal)
    {
        ["workflow-instances"] = RoundTrip<InstanceList>,
        ["workflow-instance"] = RoundTrip<InstanceDetail>,
        ["workflow-instance-fact"] = RoundTrip<FactView>,
        ["workflow-status"] = RoundTrip<StatusFields>,
        ["workflow-recipe"] = RoundTrip<RecipeView>,
    };

    private static IEnumerable<(string Slug, string Scenario, byte[] Body)> Served() =>
        from golden in GoldenFiles.All()
        where golden.Kind == "Api" && _workflowSlugs.Contains(golden.Slug, StringComparer.Ordinal) && !IsRefusal(golden.Body)
        select (golden.Slug, golden.Scenario, Encoding.UTF8.GetBytes(golden.Body));

    private static bool IsRefusal(string body) => JsonNode.Parse(body) is JsonObject json && json["traceId"] is not null;

    [Fact]
    public void Every_served_workflow_golden_is_found_on_disk_for_every_slug()
    {
        var served = Served().ToList();

        foreach (var slug in _workflowSlugs)
        {
            Assert.Contains(served, g => g.Slug == slug);
        }
    }

    [Fact]
    public void Nullable_members_are_present_and_null_never_missing()
    {
        // Every member the app mirrors is written back: a golden that left a nullable member out, or carries one the app
        // doesn't know, writes back differently. (RespectRequiredConstructorParameters makes a missing one a read error.)
        foreach (var (slug, scenario, body) in Served())
        {
            var written = _roundTrip[slug](body);

            Assert.True(written.IsSuccess, $"{slug}/{scenario}: {(written.IsError ? written.Error.Message : string.Empty)}");
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(body), JsonNode.Parse(written.Value)), $"{slug}/{scenario}: the app writes back what it read");
        }
    }

    [Fact]
    public void Workflow_status_goldens_read_as_StatusFields_and_equal_the_views_current_status()
    {
        var views = Served().Where(g => g.Slug == "workflow-instance")
            .Select(g => (g.Scenario, Status: JsonNode.Parse(g.Body)!["current"]!["status"]!)) // why: every instance golden has current.status
            .ToList();
        var matched = 0;

        foreach (var (_, scenario, body) in Served().Where(g => g.Slug == "workflow-status"))
        {
            var status = AwJson.Deserialize<StatusFields>(body, SindriApiClient.Json);
            Assert.True(status.IsSuccess, $"workflow-status/{scenario}");
            Assert.False(string.IsNullOrEmpty(status.Value.Text), $"workflow-status/{scenario}: no text");

            var sameAddress = views.Where(v => string.Equals((string?)v.Status["header"]!["address"], status.Value.Header.Address, StringComparison.Ordinal)).ToList();
            if (sameAddress.Count > 0)
            {
                var node = JsonNode.Parse(body);
                Assert.True(sameAddress.Exists(v => JsonNode.DeepEquals(v.Status, node)), $"workflow-status/{scenario} differs from the current.status of {string.Join(", ", sameAddress.Select(v => v.Scenario))}");
                matched++;
            }
        }

        Assert.True(matched > 0, "no status golden has an instance golden to equal");
    }

    [Fact]
    public void Not_found_reads_as_ProblemDetails_SND_ITEM_404()
    {
        var refusals = GoldenFiles.All().Where(g => g.Kind == "Api" && g.Scenario == "not-found" && _workflowSlugs.Contains(g.Slug, StringComparer.Ordinal)).ToList();
        Assert.True(refusals.Count >= 2, "the instance and fact routes each have a not-found golden");

        foreach (var (_, slug, _, body) in refusals)
        {
            var problem = AwJson.Deserialize<ProblemView>(Encoding.UTF8.GetBytes(body), SindriApiClient.Json).Value;
            var error = SindriApiClient.Refusal(HttpStatusCode.NotFound, Encoding.UTF8.GetBytes(body));

            Assert.Equal("SND-ITEM-404", problem.Title);
            Assert.NotNull(error);
            Assert.Equal(("SND-ITEM-404", ErrorKind.NotFound, problem.Detail), (error.Code, error.Kind, error.Message));
            Assert.False(string.IsNullOrEmpty(problem.Detail), slug);
        }
    }

    [Fact]
    public void Unknown_fact_type_reads_as_payload()
    {
        var recipe = JsonNode.Parse(GoldenFiles.Api("workflow-recipe", "workflow-project"))!; // why: the golden is a JSON object
        recipe["inputs"]![0]!["type"] = "hologram";
        recipe["outputs"]![0]!["type"] = "hologram";
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "project-build"))!; // why: as above
        instance["facts"]![0]!["type"] = "hologram";
        var fact = JsonNode.Parse(GoldenFiles.Api("workflow-instance-fact", "text"))!; // why: as above
        fact["type"] = "hologram";

        Assert.Equal(FactType.Payload, Read<RecipeView>(recipe).Inputs[0].Type);
        Assert.Equal(FactType.Payload, Read<RecipeView>(recipe).Outputs[0].Type);
        Assert.Equal(FactType.Payload, Read<InstanceDetail>(instance).Facts[0].Type);
        Assert.Equal(FactType.Payload, Read<FactView>(fact).Type);
    }

    [Fact]
    public void A_fact_type_is_read_by_its_wire_word()
    {
        var scope = Read<FactView>(JsonNode.Parse(GoldenFiles.Api("workflow-instance-fact", "scope-map"))!); // why: the golden is a JSON object

        Assert.Equal(FactType.ScopeMap, scope.Type);
    }

    private static T Read<T>(JsonNode node)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(node.ToJsonString()), SindriApiClient.Json).Value;

    private static Result<byte[]> RoundTrip<T>(byte[] body)
        where T : class
    {
        var value = AwJson.Deserialize<T>(body, SindriApiClient.Json);
        return value.IsError ? value.Error : AwJson.Serialize(value.Value, SindriApiClient.Json);
    }
}
