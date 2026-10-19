using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Line;

/// <summary>'step.design.check' on the block runtime (line spec §4.1): pass, or gaps with the findings; a fact it needs and the instance lacks fails the Step.</summary>
[Lane(TestLane.Unit)]
public sealed class DesignCheckStepTests
{
    private const string Scope = """{"scopeMap":{"surfaces":[{"id":"S1"}]},"acceptance":{"items":[{"id":"AC-1","text":"WHEN a THEN b."}]},"openQuestions":[],"answerLog":[]}""";

    private const string Design = """{"sections":[{"id":"sec-1","covers":["S1"],"cites":["S1"]}],"signals":[{"item":"AC-1","kind":"test","statement":"b holds","runs":"head"}],"decisions":[]}""";

    private static async Task<Core.Workflow.Blocks.BlockStepResult> Run(ScopingFacts facts) =>
        await new DesignCheckStep().Run(ScopingStepContexts.Of("step.design.check", facts), TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_sound_draft_passes_and_writes_nothing()
    {
        var result = await Run(new ScopingFacts { ["scope"] = Scope, ["packs"] = "{}", ["design"] = Design });

        Assert.Equal("pass", result.Result!.Outcome.Value);
        Assert.Empty(result.Result.Outputs);
    }

    [Fact]
    public async Task A_draft_with_a_gap_reports_gaps_with_the_findings()
    {
        var result = await Run(new ScopingFacts { ["scope"] = Scope, ["packs"] = "{}", ["design"] = Design.Replace("\"covers\":[\"S1\"]", "\"covers\":[]", StringComparison.Ordinal) });

        Assert.Equal("gaps", result.Result!.Outcome.Value);
        var findings = JsonNode.Parse(result.Result.Outputs[FactKey.From("fact:findings")].GetRawText())!.AsArray();
        Assert.Equal("DSN-COV-001", (string)findings[0]!["rule"]!);
    }

    [Fact]
    public async Task A_missing_fact_fails_the_Step_rather_than_passing_it()
    {
        var result = await Run(new ScopingFacts { ["scope"] = Scope, ["packs"] = "{}" });

        Assert.Null(result.Result);
        Assert.Contains("fact:design", result.Failure, StringComparison.Ordinal);
    }
}
