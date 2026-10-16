using System.Text.Json.Nodes;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Tests.ApiRun;

namespace Aw.Evidence.Tests.Runs;

/// <summary>The api summary.json shape that evidence.md documents: every key is always written, in this order, and an unknown value is null (never left out).</summary>
public sealed class ApiSummaryShapeTests
{
    private static readonly string[] _top =
    [
        "schema", "kind", "runId", "runInstance", "startedAt", "finishedAt", "appBuild", "scriptSha256", "at", "host", "outcome", "headline",
        "steps", "failures", "declaredWrites", "passedEnv", "provenance", "seedSha256", "refusal", "error", "invocations",
    ];

    private static string[] Keys(JsonNode? node) => [.. Assert.IsType<JsonObject>(node).Select(static p => p.Key)];

    private static async Task<JsonObject> Passing()
    {
        var harness = new ApiRunHarness().WithScript("""{"requests":[{"method":"GET","path":"/a","expect":{"status":200}}]}""");
        _ = harness.Transport.Reply("GET", ApiRunHarness.Origin + "/a", new HttpReply(200, Location: null, "ok"));
        return Assert.IsType<JsonObject>(JsonNode.Parse((await harness.Run()).Value.Document));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_summary_writes_every_documented_key_in_order_and_null_for_each_unknown()
    {
        var json = await Passing();

        Assert.Equal(_top, Keys(json));
        Assert.Equal(["appBuild", "seedSha256", "refusal", "error"], [.. _top.Where(key => json[key] is null)]);
        Assert.Equal(["evidence.api.summary/1", "api", "unseeded", "passed"], [json["schema"]!.GetValue<string>(), json["kind"]!.GetValue<string>(), json["provenance"]!.GetValue<string>(), json["outcome"]!.GetValue<string>()]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_writes_its_documented_keys()
    {
        var json = await Passing();

        Assert.Equal(["index", "action", "target", "status", "code", "output", "headerNames"], Keys(json["steps"]![0]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failure_and_a_refusal_write_their_documented_keys()
    {
        var harness = new ApiRunHarness().WithScript("""{"requests":[{"method":"GET","path":"/a","expect":{"status":201}}]}""");
        _ = harness.Transport.Reply("GET", ApiRunHarness.Origin + "/a", new HttpReply(200, Location: null, "ok"));
        var failed = JsonNode.Parse((await harness.Run()).Value.Document)!;
        var refused = JsonNode.Parse((await harness.Run(ApiRunHarness.Request(host: null))).Value.Document)!;

        Assert.Equal(["step", "pointer", "expected", "actual"], Keys(failed["failures"]![0]));
        Assert.Equal(["code", "message"], Keys(refused["refusal"]));
    }
}
