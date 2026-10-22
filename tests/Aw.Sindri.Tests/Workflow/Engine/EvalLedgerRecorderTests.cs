using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// 'recipe eval --record' from the ledger (spec §6.3, §9.2): a row's delta holds only the facts a report added or changed, so a
/// Step that reported a required output unchanged still gets a line that carries it, from the facts the instance kept.
/// </summary>
public sealed class EvalLedgerRecorderTests
{
    private const string Instance = "WFI-00000000000000000000000000000001";

    private static readonly Core.Workflow.Recipes.Recipe _scoping = EvalComposition.Recipe("sindri/workflow.scoping.yaml");

    [Fact, Trait("Lane", "unit")]
    public void An_apply_that_changed_only_the_open_questions_still_reports_the_map_and_the_acceptance_list()
    {
        var contents = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var map = Stored(contents, """{"surfaces":[]}""");
        var acceptance = Stored(contents, """{"items":[]}""");
        var before = Stored(contents, "[]");
        var after = Stored(contents, """[{"id":"Q1"}]""");
        var history = new[]
        {
            Row("start", from: "", to: "apply", outcome: null, new Dictionary<string, string> { ["fact:scopeMap"] = map, ["fact:acceptance"] = acceptance, ["fact:openQuestions"] = before }),
            Row("step.completed", from: "apply", to: "check", outcome: "updated", new Dictionary<string, string> { ["fact:openQuestions"] = after }),
        };

        var recorded = EvalLedgerRecorder.Record("recorded", _scoping, InMemoryChildRecipes.None, EvalComposition.Catalog, history, contents);

        Assert.False(recorded.IsError, recorded.IsError ? recorded.Error.Message : null);
        var outputs = Assert.IsAssignableFrom<IReadOnlyDictionary<FactKey, JsonElement>>(recorded.Value.Steps[0].Trigger.Outputs);
        Assert.Equal(["fact:acceptance", "fact:openQuestions", "fact:scopeMap"], outputs.Keys.Select(static k => k.Value).Order(StringComparer.Ordinal));
        Assert.Equal("""{"items":[]}""", outputs[FactKey.From("fact:acceptance")].GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_fact_the_Step_does_not_write_is_not_added_to_its_line()
    {
        var contents = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var brief = Stored(contents, "the brief");
        var history = new[]
        {
            Row("start", from: "", to: "record", outcome: null, new Dictionary<string, string> { ["fact:brief"] = brief, ["fact:answerLog"] = Stored(contents, "[]") }),
            Row("step.completed", from: "record", to: "apply", outcome: "recorded", facts: null),
        };

        var recorded = EvalLedgerRecorder.Record("recorded", _scoping, InMemoryChildRecipes.None, EvalComposition.Catalog, history, contents);

        Assert.False(recorded.IsError, recorded.IsError ? recorded.Error.Message : null);
        Assert.Equal(["fact:answerLog"], recorded.Value.Steps[0].Trigger.Outputs!.Keys.Select(static k => k.Value));
    }

    private static string Stored(Dictionary<string, JsonElement> contents, string text)
    {
        var sha = EngineFixture.Sha(text);
        contents[sha] = EvalLedgerRecorder.ContentOf(Encoding.UTF8.GetBytes(text));
        return sha;
    }

    private static WorkflowEventPayload Row(string trigger, string from, string to, string? outcome, IReadOnlyDictionary<string, string>? facts) => new(
        Instance, from, to, trigger, outcome, Run: null,
        new WorkflowEventDetail(
            TriggerKey.For(WorkflowInstanceId.From(Instance), "t"), "agent:session", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null, Answer: null, Amount: null, Reason: null,
            Gates: [], Evidence: [], Grant: null, Override: null, Refusal: null, Effects: [],
            new WorkflowEventDelta(new FrameSummary(to, Entry: 1, Run: null, Child: null), Suspension: null, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: facts, Outcome: null)));
}
