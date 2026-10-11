using System.Text.Json.Nodes;
using Aw.Evidence.Core.Lint;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Steps;
using Aw.Evidence.Core.Usage;
using Aw.Evidence.Core.Visual;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Runs;

/// <summary>
/// The summary.json shape that evidence.md documents for Scorer and bugfix-state: every key is always written, in this
/// order, and an unknown value is null (never left out), so a reader's schema must accept null where the doc says so.
/// </summary>
public sealed class RunSummaryShapeTests
{
    private static readonly string[] _top =
    [
        "schema", "runId", "runInstance", "startedAt", "finishedAt", "route", "role", "host", "appBuild", "fixtures",
        "scriptSha256", "pr", "viewports", "steps", "visual", "lintFindings", "invocations", "planning", "evidence",
    ];

    private static readonly string[] _nullable = ["appBuild", "fixtures", "pr", "planning"];

    private static RunSummary Full() =>
        A.RunSummary().WithStep(StepStatus.Failed).WithLint(LintRule.Overlap)
            .WithVisual(new RunVisual(Verdict.LooksOff, ["shifted"], DiffScore: 0.25, DiffImage: "/runs/run-1/visual-diff.png"))
            .WithEvidence(new RunEvidence(["/runs/run-1/desktop-trace.zip"], ["/runs/run-1/v.webm"])).Build() with
        {
            AppBuild = "abc123",
            Fixtures = "seed-1",
            Pr = "158",
            Planning = new PlanningMeta("158", "planner", 1200, 300, 40),
            Invocations = [new ModelInvocation(InvocationPhase.Planning, "planner", 1200, Ok: true, 300, 40, CacheHit: false)],
        };

    private static JsonObject Write(RunSummary summary) => Assert.IsType<JsonObject>(JsonNode.Parse(TestJson.Write(summary)));

    private static string[] Keys(JsonNode? node) => [.. Assert.IsType<JsonObject>(node).Select(static p => p.Key)];

    [Fact, Trait("Lane", "unit")]
    public void A_full_summary_writes_every_documented_key_with_a_value()
    {
        var json = Write(Full());

        Assert.Equal(_top, Keys(json));
        Assert.All(_top, key => Assert.NotNull(json[key]));
        Assert.Equal(["viewport", "index", "action", "target", "status", "screenshot"], Keys(json["steps"]![0]));
        Assert.Equal(["verdict", "reasons", "diffScore", "diffImage"], Keys(json["visual"]));
        Assert.All(Keys(json["visual"]), key => Assert.NotNull(json["visual"]![key]));
        Assert.Equal(["viewport", "rule", "selector", "detail"], Keys(json["lintFindings"]![0]));
        Assert.Equal(["phase", "model", "elapsedMs", "ok", "inputTokens", "outputTokens", "cacheHit"], Keys(json["invocations"]![0]));
        Assert.Equal(["pr", "model", "elapsedMs", "inputTokens", "outputTokens"], Keys(json["planning"]));
        Assert.Equal(["traces", "videos"], Keys(json["evidence"]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_minimal_summary_still_writes_every_key_and_null_for_each_unknown()
    {
        var json = Write(A.RunSummary().WithVisual(new RunVisual(Verdict.Unchecked, [], DiffScore: null, DiffImage: null)).Build());

        Assert.Equal(_top, Keys(json));
        Assert.All(_nullable, key => Assert.Null(json[key]));
        Assert.All(_top.Except(_nullable, StringComparer.Ordinal), key => Assert.NotNull(json[key]));
        Assert.Equal(["verdict", "reasons", "diffScore", "diffImage"], Keys(json["visual"]));
        Assert.Null(json["visual"]!["diffScore"]);
        Assert.Null(json["visual"]!["diffImage"]);
        Assert.Empty(json["lintFindings"]!.AsArray());
        Assert.Empty(json["invocations"]!.AsArray());
        Assert.Equal(["traces", "videos"], Keys(json["evidence"]));
    }
}
