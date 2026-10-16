using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// An answer wait (spec §3.4, §5.8): 'workflow answer --text' satisfies it as 'wait.satisfied' with outcome 'answered',
/// records 'fact:answer', and commits under the answer's key with the answer as the event's trigger.
/// </summary>
public sealed class AnswerWaitTests
{
    /// <summary>A recipe whose initial state waits for an answer, with a transition on outcome 'answered'.</summary>
    public static string Yaml { get; } = string.Join('\n',
    [
        "schema: 1",
        "id: workflow.ask",
        "version: 1",
        "inputs: [{ key: fact:item, type: item, kind: item }]",
        "initial: asking",
        "states:",
        "  asking: { kind: wait, timeout: { after: 1d, then: park }, waitFor: { answer: { id: scope, question: \"Which scope?\" } },",
        "            next: \"Waiting for your answer to: Which scope?\" }",
        "  done:   { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: asking, to: done, on: wait.satisfied, when: { outcome: answered } }",
        string.Empty,
    ]);

    private static readonly Lazy<Recipe> _ask = new(static () => Read(RecipeComposition.ReadClean(Yaml, "workflow.ask.yaml")));

    [Fact, Trait("Lane", "unit")]
    public void Text_satisfies_the_answer_wait_and_records_the_answer()
    {
        var asking = At(_ask.Value, "asking");
        var answer = Answer(AnswerVariant.Text, "Only the CLI.");

        var applied = Assert.IsType<Applied>(Decide(asking, _ask.Value, answer, rules: EngineRules.Default));

        Assert.Equal(answer.KeyFor(Id), applied.Key);
        var change = Assert.Single(applied.Changes);
        Assert.Equal(("done", "resolved"), (change.After.State, change.After.Outcome?.Value));
        Assert.Equal(Fact("Only the CLI.").Sha256, change.After.Facts[FactKey.From("fact:answer")].Sha256);
        Assert.Equal((WorkflowEventType.Transition, "answer", answer.KeyFor(Id)), (change.Event.Type, change.Event.Trigger.Kind, change.Event.Trigger.Key));
        Assert.Contains(FactKey.From("fact:answer"), Assert.IsType<IReadOnlyDictionary<FactKey, FactValue>>(change.Event.Delta.Facts, exactMatch: false).Keys);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(AnswerVariant.Resume)]
    [InlineData(AnswerVariant.Accept)]
    [InlineData(AnswerVariant.Grant)]
    public void Another_variant_on_an_answer_wait_is_SND_WF_003_naming_text(AnswerVariant variant)
    {
        var rejected = Assert.IsType<Rejected>(Decide(At(_ask.Value, "asking"), _ask.Value, Answer(variant, text: null, amount: 1), rules: EngineRules.Default));

        Assert.Equal("SND-WF-003", rejected.Error.Code);
        Assert.Contains("--text", rejected.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Match_reads_a_text_answer_on_an_answer_wait_as_its_wait_satisfied_transition()
    {
        var match = TransitionDecision.Match(At(_ask.Value, "asking"), _ask.Value, Answer(AnswerVariant.Text, "Only the CLI."));

        Assert.Equal(0, match.Transition);
    }

    private static Recipe Read(RecipeDocument document) => document.Recipe with { Hash = document.Hash };

    private static AnswerGiven Answer(AnswerVariant variant, string? text, int? amount = null) => new(
        "human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), Sha(variant.ToString())), variant, text, amount, Forwarded: null);
}
