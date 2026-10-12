using System.Text.Json;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// 'recipe eval --record' writes each answer with the variant that fitted what it answered (spec §6.3, §9.2): a text
/// answer on an answer wait, '--resume' (with any text) on a needs-answer (wait) park, '--accept' or '--reject' on an
/// escalated gate. Every case here is recorded from a replayed history and replays again; the answer wait's
/// transition is gated, so the eval runs its gates from the answer line (spec §3.4, §5.5, §5.8).
/// </summary>
public sealed class EvalRecorderAnswerTests
{
    private const string Start = """{"start":{"inputs":{}}}""";
    private const string Done = """{"state":"done","outcome":"resolved"}""";
    private const string WaitPark = """{"state":"parked","park":"needs-answer"}""";

    [Fact, Trait("Lane", "unit")]
    public void A_text_answer_on_an_answer_wait_whose_gate_refuses_is_recorded_as_text()
    {
        var recorded = RoundTrip(Gated("gate.test.check"),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Only the CLI."}}""", """{"gate.test.check":"fail"}""", WaitPark),
            Line("""{"kind":"answer","variant":"resume"}""", gates: null, Done));

        Assert.Equal([EvalVariant.Text, EvalVariant.Resume], recorded.Steps.Select(s => s.Trigger.Variant));
        Assert.Equal("Only the CLI.", Text(recorded.Steps[0]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_text_answer_on_an_answer_wait_that_escalates_is_recorded_as_text_and_the_accept_as_accept()
    {
        var recorded = RoundTrip(Gated("gate.test.model"),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Only the CLI."}}""", """{"gate.test.model":"uncertain"}""", """{"state":"escalated"}"""),
            Line("""{"kind":"answer","variant":"accept"}""", gates: null, Done));

        Assert.Equal([EvalVariant.Text, EvalVariant.Accept], recorded.Steps.Select(s => s.Trigger.Variant));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reject_on_an_escalated_answer_wait_is_recorded_as_reject()
    {
        var recorded = RoundTrip(Gated("gate.test.model"),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Only the CLI."}}""", """{"gate.test.model":"uncertain"}""", """{"state":"escalated"}"""),
            Line("""{"kind":"answer","variant":"reject"}""", gates: null, WaitPark));

        Assert.Equal([EvalVariant.Text, EvalVariant.Reject], recorded.Steps.Select(s => s.Trigger.Variant));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_resume_on_a_needs_answer_wait_park_that_is_refused_again_is_recorded_as_resume()
    {
        var recorded = RoundTrip(Gated("gate.test.check"),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Only the CLI."}}""", """{"gate.test.check":"fail"}""", WaitPark),
            Line("""{"kind":"answer","variant":"resume"}""", """{"gate.test.check":"fail"}""", WaitPark),
            Line("""{"kind":"answer","variant":"resume"}""", gates: null, Done));

        Assert.Equal([EvalVariant.Text, EvalVariant.Resume, EvalVariant.Resume], recorded.Steps.Select(s => s.Trigger.Variant));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_resume_with_text_on_a_needs_answer_wait_park_is_recorded_as_resume_with_its_text()
    {
        var recorded = RoundTrip(Gated("gate.test.check"),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Only the CLI."}}""", """{"gate.test.check":"fail"}""", WaitPark),
            Line("""{"kind":"answer","variant":"resume","outputs":{"fact:answer":"The CLI and the docs."}}""", gates: null, Done));

        Assert.Equal([EvalVariant.Text, EvalVariant.Resume], recorded.Steps.Select(s => s.Trigger.Variant));
        Assert.Equal("The CLI and the docs.", Text(recorded.Steps[1]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_text_answer_repeating_the_last_answer_is_recorded_as_text_with_that_text()
    {
        var recorded = RoundTrip(TwoQuestions(),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Yes."}}""", gates: null, """{"state":"again"}"""),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Yes."}}""", gates: null, Done));

        Assert.Equal([EvalVariant.Text, EvalVariant.Text], recorded.Steps.Select(s => s.Trigger.Variant));
        Assert.Equal("Yes.", Text(recorded.Steps[1]));
    }

    /// <summary>The original case replays; its history, recorded with only the events' deltas (no store report), replays too.</summary>
    private static EvalCase RoundTrip(Recipe recipe, params string[] lines)
    {
        var original = Assert.IsType<EvalCase>(EvalCaseReader.Read("case", string.Join('\n', [Start, .. lines]), EvalComposition.Options).Case);
        var (verdict, events) = EvalRunner.Replay(original, recipe, EngineFixture.Catalog, InMemoryChildRecipes.None);
        Assert.Null(verdict.Detail);

        var recorded = EvalRecorder.Record("recorded", events, new Dictionary<TriggerKey, IReadOnlyDictionary<FactKey, FactValue>>(), Contents(original)).Value;

        Assert.Null(EvalRunner.Run(recorded, recipe, EngineFixture.Catalog, InMemoryChildRecipes.None).Detail);
        Assert.Equal(original.Steps.Select(s => s.Expect.State), recorded.Steps.Select(s => s.Expect.State), StringComparer.Ordinal);
        return recorded;
    }

    private static string Line(string trigger, string? gates, string expect) =>
        "{\"trigger\":" + trigger + (gates is null ? string.Empty : ",\"gates\":" + gates) + ",\"expect\":" + expect + "}";

    private static string? Text(EvalStepLine line) =>
        line.Trigger.Outputs?.GetValueOrDefault(FactKey.From("fact:answer")).GetString();

    /// <summary>The answer-wait recipe with <paramref name="gate"/> on 'asking -> done'.</summary>
    private static Recipe Gated(string gate)
    {
        var ask = Ask();
        var use = new GateUse(BlockId.From(gate), Threshold: gate == "gate.test.model" ? 0.8 : null, new Dictionary<string, WithValue>(StringComparer.Ordinal));
        return ask with { Transitions = [ask.Transitions[0] with { Gates = [use] }] };
    }

    /// <summary>Two answer waits in a row: 'asking' then 'again', then done.</summary>
    private static Recipe TwoQuestions()
    {
        var ask = Ask();
        var asking = ask.States["asking"];
        var again = asking with { WaitFor = new WaitFor(Event: null, new AnswerWait(AnswerId.From("again"), "Still sure?"), Resource: null, Child: null) };
        var states = new Dictionary<string, RecipeState>(ask.States, StringComparer.Ordinal) { ["again"] = again };
        return ask with
        {
            States = states,
            Transitions = [ask.Transitions[0] with { To = "again" }, ask.Transitions[0] with { From = ["again"] }],
        };
    }

    private static Recipe Ask()
    {
        var document = RecipeComposition.ReadClean(AnswerWaitTests.Yaml, "workflow.ask.yaml");
        return document.Recipe with { Hash = document.Hash };
    }

    private static Dictionary<string, JsonElement> Contents(EvalCase evalCase)
    {
        var contents = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var content in evalCase.Steps.SelectMany(s => s.Trigger.Outputs?.Values ?? []))
        {
            contents[EvalFacts.Value(content).Ref] = content;
        }

        return contents;
    }
}
