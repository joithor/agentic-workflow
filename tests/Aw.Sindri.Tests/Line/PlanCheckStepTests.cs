using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Line;

/// <summary>'step.plan.check' on the block runtime (line spec §6.1): pass, or gaps with the findings; a fact it needs and the instance lacks fails the Step.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanCheckStepTests
{
    private const string Design = """{"acceptance":{"items":[{"id":"AC-1","text":"a"}]},"signals":[{"item":"AC-1","kind":"api-transcript","statement":"returns 410","runs":"head"}]}""";

    private const string Plan = """{"phases":[{"id":"P1","deps":[],"size":"S","kind":"feature","acceptance":[{"id":"AC-1","evidence":["api-transcript"]}]}]}""";

    private static async Task<BlockStepResult> Run(ScopingFacts facts) =>
        await new PlanCheckStep().Run(ScopingStepContexts.Of("step.plan.check", facts), TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_sound_plan_passes_and_writes_nothing()
    {
        var result = await Run(new ScopingFacts { ["scopePacket"] = "sha256:aa", ["design"] = Design, ["plan"] = Plan });

        Assert.Equal("pass", result.Result!.Outcome.Value);
        Assert.Empty(result.Result.Outputs);
    }

    [Fact]
    public async Task A_plan_with_a_gap_reports_gaps_with_the_findings()
    {
        var result = await Run(new ScopingFacts { ["scopePacket"] = "sha256:aa", ["design"] = Design, ["plan"] = Plan.Replace("\"size\":\"S\"", "\"size\":\"XL\"", StringComparison.Ordinal) });

        Assert.Equal("gaps", result.Result!.Outcome.Value);
        var findings = JsonNode.Parse(result.Result.Outputs[FactKey.From("fact:findings")].GetRawText())!.AsArray();
        Assert.Equal("PLN-SIZE-001", (string)findings[0]!["rule"]!);
    }

    [Fact]
    public async Task A_missing_fact_fails_the_Step_rather_than_passing_it()
    {
        var result = await Run(new ScopingFacts { ["scopePacket"] = "sha256:aa", ["design"] = Design });

        Assert.Null(result.Result);
        Assert.Contains("fact:plan", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_plan_fact_that_is_not_JSON_fails_the_Step_rather_than_reading_as_empty()
    {
        var result = await Run(new ScopingFacts { ["scopePacket"] = "sha256:aa", ["design"] = Design, ["plan"] = "the plan is ready" });

        Assert.Null(result.Result);
        Assert.Contains("fact:plan", result.Failure, StringComparison.Ordinal);
    }
}
