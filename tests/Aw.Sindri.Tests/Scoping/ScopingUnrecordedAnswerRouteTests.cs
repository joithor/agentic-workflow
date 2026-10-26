using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// An answer no acceptance item, open question or map change cites (SCP-ACC-003, #1205) is not something a redraft can fix: evidence reports 'open' for it, which goes to the owner's stop with the
/// finding open, the way a repeated list does. Any other failing rule still goes back to the drafter, and an uncovered file (SCP-COV-003) still goes to the exclusions step first.
/// </summary>
public sealed class ScopingUnrecordedAnswerRouteTests
{
    private const string EvidenceEval = "sindri/evals/workflow.scoping/evidence-incomplete.jsonl";

    // why: the gold answer log has an entry; renumbered to A9, no item, surface or open question cites it and it names no recordedAs
    private static ScopingFacts Unrecorded() => ScopingFacts.Expiry().Edit("answerLog", n =>
    {
        n[0]!["id"] = "A9";
        n[0]!["recordedAs"] = new JsonArray();
    });

    private static ScopingDeskResponse Evidence(ScopingFacts facts)
    {
        var ran = DeskBlocks.Run("step.scope.evidence", new ScopeFacts(facts));
        Assert.False(ran.IsError, ran.IsError ? ran.Error.Message : null);
        return ran.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_with_only_SCP_ACC_003_failing_reports_open_with_the_packet_and_the_findings()
    {
        var response = Evidence(Unrecorded());

        Assert.Equal("open", response.Outcome);
        Assert.Equal(["SCP-ACC-003"], response.Findings.Select(static f => f.Rule).Distinct(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Contains("findings", response.Outputs.Keys, StringComparer.Ordinal);
        Assert.Contains("roundLog", response.Outputs.Keys, StringComparer.Ordinal);
        Assert.Contains("scopeMap", response.Outputs.Keys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_with_SCP_ACC_003_and_another_rule_failing_stays_incomplete()
    {
        var facts = Unrecorded().Edit("acceptance", n => n["items"]![1]!["id"] = "AC-1");   // why: the gold acceptance list has two or more items

        Assert.Equal("incomplete", Evidence(facts).Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_open_scope_goes_to_the_stop()
    {
        var lines = ShippedRecipes.Read(EvidenceEval).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var first = Array.FindIndex(lines, static l => l.Contains("\"outcome\":\"incomplete\"", StringComparison.Ordinal));
        var open = JsonNode.Parse(lines[first])!;   // why: the eval line is JSON
        open["trigger"]!["outcome"] = "open";   // why: a trigger line has a trigger object
        open["expect"] = JsonNode.Parse("""{"state":"stop"}""");
        var read = EvalCaseReader.Read("open", string.Join('\n', [.. lines[..first], open.ToJsonString()]), EvalComposition.Options);
        var evalCase = Assert.IsType<EvalCase>(read.Case);
        var recipe = EvalComposition.Recipe("sindri/workflow.scoping.yaml");

        var verdict = EvalRunner.Run(evalCase, recipe, EvalComposition.Catalog, InMemoryChildRecipes.None);

        Assert.Null(verdict.Detail);
    }
}
