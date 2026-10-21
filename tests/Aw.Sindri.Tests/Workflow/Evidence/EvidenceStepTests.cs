using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Evidence;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Evidence;

/// <summary>The evidence blocks' two pure Steps on the block runtime (decision-tree spec §3.3, §5.3).</summary>
[Lane(TestLane.Unit)]
public sealed class EvidenceStepTests
{
    private static JsonNode Summary(BlockStepResult result) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:evidenceSummary")].GetRawText())!;

    private static Dictionary<string, string> Parts() => new(StringComparer.Ordinal)
    {
        ["evidenceUi"] = """{"result":"passed","artifacts":2}""",
        ["evidenceApi"] = """{"result":"passed","checks":5}""",
        ["evidenceDb"] = "\"three rows held\"",
    };

    [Fact]
    public async Task None_records_that_the_always_run_checks_are_the_proof_for_the_built_change()
    {
        var result = await new EvidenceNoneStep().Run(BuildShipContexts.Of("step.evidence.none", new Dictionary<string, string>(StringComparer.Ordinal) { ["built"] = """{"head":"abc"}""" }), TestContext.Current.CancellationToken);

        Assert.Equal("done", result.Result!.Outcome.Value);
        Assert.Equal(["evidenceSummary"], [.. result.Result.Outputs.Keys.Select(static k => k.Value[5..])], StringComparer.Ordinal);
        Assert.Equal("none", (string?)Summary(result)["kind"]);
        Assert.Equal("abc", (string?)Summary(result)["built"]!["head"]);
        Assert.Contains("always-run checks", (string?)Summary(result)["text"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task None_without_the_built_fact_fails_the_Step_naming_it()
    {
        var result = await new EvidenceNoneStep().Run(BuildShipContexts.Of("step.evidence.none", []), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("fact:built", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Combine_keeps_the_three_summaries_each_under_its_own_name()
    {
        var result = await new EvidenceCombineStep().Run(BuildShipContexts.Of("step.evidence.combine", Parts()), TestContext.Current.CancellationToken);

        Assert.Equal("done", result.Result!.Outcome.Value);
        Assert.Equal("combined", (string?)Summary(result)["kind"]);
        Assert.Equal(2, (int?)Summary(result)["ui"]!["artifacts"]);
        Assert.Equal(5, (int?)Summary(result)["api"]!["checks"]);
        Assert.Equal("three rows held", (string?)Summary(result)["db"]);
    }

    [Theory]
    [InlineData("evidenceUi")]
    [InlineData("evidenceApi")]
    [InlineData("evidenceDb")]
    public async Task Combine_with_a_summary_missing_fails_the_Step_naming_it(string missing)
    {
        var facts = Parts();
        _ = facts.Remove(missing);

        var result = await new EvidenceCombineStep().Run(BuildShipContexts.Of("step.evidence.combine", facts), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("fact:" + missing, result.Failure, StringComparison.Ordinal);
    }

    private const string Unselected = """{"selected":false,"kind":"ui","reason":"no screen to check"}""";

    [Fact]
    public async Task Combine_for_a_phase_with_only_api_evidence_keeps_the_api_part_and_leaves_the_others_out()
    {
        var facts = Parts();
        facts["evidenceUi"] = Unselected;
        facts["evidenceDb"] = Unselected.Replace("ui", "db", StringComparison.Ordinal);

        var result = await new EvidenceCombineStep().Run(BuildShipContexts.Of("step.evidence.combine", facts), TestContext.Current.CancellationToken);

        Assert.Equal("done", result.Result!.Outcome.Value);
        Assert.Equal(["api", "kind"], [.. Summary(result).AsObject().Select(static p => p.Key).Order(StringComparer.Ordinal)], StringComparer.Ordinal);
        Assert.Equal(5, (int?)Summary(result)["api"]!["checks"]);
    }

    [Fact]
    public async Task Combine_when_every_part_is_unselected_fails_and_says_to_route_to_evidence_none()
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["evidenceUi"] = Unselected,
            ["evidenceApi"] = Unselected.Replace("ui", "api", StringComparison.Ordinal),
            ["evidenceDb"] = Unselected.Replace("ui", "db", StringComparison.Ordinal),
        };

        var result = await new EvidenceCombineStep().Run(BuildShipContexts.Of("step.evidence.combine", facts), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Equal("no evidence target in this phase; route it to evidence.none", result.Failure);
    }

    [Fact]
    public async Task Combine_with_a_malformed_selection_marker_fails_naming_the_part()
    {
        var facts = Parts();
        facts["evidenceApi"] = """{"selected":"no"}""";

        var result = await new EvidenceCombineStep().Run(BuildShipContexts.Of("step.evidence.combine", facts), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("fact:evidenceApi", result.Failure, StringComparison.Ordinal);
    }
}
