using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// 'recipe eval --record' (spec §6.3, §9.2): a live instance's history written as an eval case. Each history entry is
/// one ledger event with the instance after it: the events give the triggers and gate verdicts, the instances give the
/// states each line expects. A line's outputs are the facts its trigger reported, as the interpreter saved them, and
/// their content comes from the stored facts, never from the events, which hold only references (an event's delta
/// leaves out a fact reported again unchanged). Reading a live instance's history lands with the interpreter (P4).
/// </summary>
public static class EvalRecorder
{
    private static readonly FactKey _answer = FactKey.From("fact:answer");

    /// <summary>The case that replays <paramref name="history"/>.</summary>
    /// <param name="name">The case's name.</param>
    /// <param name="history">The instance's ledger events, oldest first from its workflow.started, each with the instance after it.</param>
    /// <param name="reported">The facts each trigger reported, by its key, as the interpreter saved them; a trigger missing here gives the facts its event's delta added.</param>
    /// <param name="contents">The stored fact content, by <see cref="FactValue.Ref"/>.</param>
    /// <returns>The case, or why the history can't be one: one of another instance, a missing fact's content, or a trigger an eval line can't name.</returns>
    public static Result<EvalCase> Record(
        string name,
        IReadOnlyList<InstanceChange> history,
        IReadOnlyDictionary<TriggerKey, IReadOnlyDictionary<FactKey, FactValue>> reported,
        IReadOnlyDictionary<string, JsonElement> contents)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(reported);
        ArgumentNullException.ThrowIfNull(contents);
        if (history.Count == 0 || history[0].Event.Type != WorkflowEventType.Started)
        {
            return KernelErrors.Invalid.With("the history doesn't start with workflow.started (fix: record from the instance's first event)");
        }

        var row = history[0].After;
        var inputs = Contents(row.Facts, contents);
        if (inputs.IsError)
        {
            return inputs.Error;
        }

        var steps = new List<EvalStepLine>(history.Count - 1);
        for (var i = 1; i < history.Count; i++)
        {
            var (recorded, after) = (history[i].Event, history[i].After);
            if (recorded.Instance != row.Id || after.Id != row.Id)
            {
                return KernelErrors.Invalid.With(
                    $"event {Text(i + 1)} of the history is for {recorded.Instance.Value}, not {row.Id.Value} (fix: record one instance's own events)");
            }

            var step = Step(new Recorded(recorded, row, after, i + 1), reported.GetValueOrDefault(recorded.Trigger.Key) ?? recorded.Delta.Facts, contents);
            if (step.IsError)
            {
                return step.Error;
            }

            steps.Add(step.Value);
            row = after;
        }

        return new EvalCase(name, new EvalStartLine(new EvalStart(inputs.Value), Scaffold: null), steps);
    }

    private static Result<EvalStepLine> Step(Recorded step, IReadOnlyDictionary<FactKey, FactValue>? reported, IReadOnlyDictionary<string, JsonElement> contents)
    {
        var facts = Contents(reported ?? new Dictionary<FactKey, FactValue>(), contents);
        if (facts.IsError)
        {
            return facts.Error;
        }

        var trigger = Trigger(step, facts.Value, contents);
        if (trigger.IsError)
        {
            return trigger.Error;
        }

        var after = step.After;
        var gates = step.Event.Gates.Where(static g => g.Verdict is not null).ToDictionary(static g => g.Gate, static g => g.Verdict!.Value); // why: the Where keeps answered gates
        var expect = new EvalExpectation(after.State, after.Park, after.Outcome);
        return new EvalStepLine(trigger.Value, expect, gates.Count == 0 ? null : gates);
    }

    private static Result<EvalTrigger> Trigger(Recorded step, Dictionary<FactKey, JsonElement> facts, IReadOnlyDictionary<string, JsonElement> contents)
    {
        var (recorded, _, after, _) = step;
        var kind = recorded.Trigger.Kind;
        var outputs = facts.Count == 0 ? null : facts;
        return kind switch
        {
            TriggerNames.StepCompleted or TriggerNames.WaitSatisfied =>
                new EvalTrigger(kind, recorded.Trigger.Outcome, Without(outputs, _answer), Park: null),
            TriggerNames.StepFailed => new EvalTrigger(kind, Outcome: null, Without(outputs, _answer), Park: null, Reason: recorded.Trigger.Reason),
            TriggerNames.ChildParked => new EvalTrigger(kind, Outcome: null, Outputs: null, after.Park),
            TriggerNames.ChildResumed or TriggerNames.ChildEscalated => new EvalTrigger(kind, Outcome: null, Outputs: null, Park: null),
            TriggerNames.Answer => Answer(step, facts, contents),
            TriggerNames.Deadline or TriggerNames.TakeoverIdle => new EvalTrigger(TriggerNames.Deadline, Outcome: null, Outputs: null, Park: null, EvalVariant.Deadline),
            _ when EvalTriggerRules.For(kind) is { } variant => new EvalTrigger(kind, Outcome: null, Outputs: null, Park: null, variant),
            _ => KernelErrors.Invalid.With($"{kind} at event {Text(step.Number)} is an engine wake an eval line can't name (fix: record a history without it)"),
        };
    }

    /// <summary>
    /// An answer's option (spec §9.2), derived from the engine's own fit table (<see cref="AnswerFit"/>) for what it
    /// answered, so the two can't drift: with nothing suspended, the answer to an answer wait ('text'); otherwise the
    /// option of the suspension's row that the event shows. A text answer's text comes from the facts its trigger
    /// reported, or from the instance after it when the delta leaves out a text repeated unchanged.
    /// </summary>
    private static Result<EvalTrigger> Answer(Recorded step, Dictionary<FactKey, JsonElement> facts, IReadOnlyDictionary<string, JsonElement> contents)
    {
        var (recorded, before, after, number) = step;
        var chosen = Chosen(before, recorded, number);
        if (chosen.IsError)
        {
            return chosen.Error;
        }

        var (variant, amount) = chosen.Value;
        var text = facts.TryGetValue(_answer, out var given) ? new Dictionary<FactKey, JsonElement> { [_answer] = given } : null;
        if (text is null && variant == EvalVariant.Text && after.Facts.TryGetValue(_answer, out var kept))
        {
            if (!contents.TryGetValue(kept.Ref, out var content))
            {
                return KernelErrors.Invalid.With($"no stored content for {_answer.Value} ({kept.Ref}) (fix: record while the instance's facts are kept)");
            }

            text = new Dictionary<FactKey, JsonElement> { [_answer] = content };
        }

        return new EvalTrigger(TriggerNames.Answer, Outcome: null, text, Park: null, variant, amount);
    }

    /// <summary>
    /// The option that fitted <paramref name="before"/>'s suspension: of the variants its §3.8 row lets fit, a grant the
    /// event recorded, an override ('accept'), a refusal ('reject'), or else the first that takes no amount. A gate
    /// escalation whose pending park took effect records neither an override nor a refusal; both of its options end it
    /// the same way, so 'accept' replays it.
    /// </summary>
    private static Result<(EvalVariant Variant, int? Amount)> Chosen(WorkflowInstance before, WorkflowEvent recorded, int number)
    {
        if (before.Suspension is not { } suspension)
        {
            return (EvalVariant.Text, null);
        }

        var fits = suspension.Mirrors is null ? AnswerVariants.Fitting(suspension) : [];
        (AnswerVariant Variant, int? Amount)? chosen = recorded switch
        {
            { Grant.Rounds: { } rounds } when fits.Contains(AnswerVariant.Grant) => (AnswerVariant.Grant, rounds),
            { Grant.Tokens: { } tokens } when fits.Contains(AnswerVariant.GrantTokens) => (AnswerVariant.GrantTokens, tokens),
            { Override: not null } when fits.Contains(AnswerVariant.Accept) => (AnswerVariant.Accept, null),
            { Type: WorkflowEventType.Refused } when fits.Contains(AnswerVariant.Reject) => (AnswerVariant.Reject, null),
            _ => fits.Where(static v => v is not (AnswerVariant.Grant or AnswerVariant.GrantTokens)).Select(static v => ((AnswerVariant, int?)?)(v, null)).FirstOrDefault(),
        };
        return chosen is var (variant, amount)
            ? (Of(variant), amount)
            : KernelErrors.Invalid.With(
                $"the answer at event {Text(number)} answered a suspension no answer variant fits (fix: record a history whose answers fit what they answered)");
    }

    private static EvalVariant Of(AnswerVariant variant) => variant switch
    {
        AnswerVariant.Text => EvalVariant.Text,
        AnswerVariant.Grant => EvalVariant.Grant,
        AnswerVariant.GrantTokens => EvalVariant.GrantTokens,
        AnswerVariant.Accept => EvalVariant.Accept,
        AnswerVariant.Reject => EvalVariant.Reject,
        AnswerVariant.Resume or _ => EvalVariant.Resume,
    };

    /// <summary>One event of the history with the instance before and after it, and its place in the history (from 1).</summary>
    private sealed record Recorded(WorkflowEvent Event, WorkflowInstance Before, WorkflowInstance After, int Number);

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static Dictionary<FactKey, JsonElement>? Without(Dictionary<FactKey, JsonElement>? outputs, FactKey fact)
    {
        if (outputs is null || !outputs.ContainsKey(fact))
        {
            return outputs;
        }

        var kept = outputs.Where(o => o.Key != fact).ToDictionary(static o => o.Key, static o => o.Value);
        return kept.Count == 0 ? null : kept;
    }

    private static Result<Dictionary<FactKey, JsonElement>> Contents(IReadOnlyDictionary<FactKey, FactValue> facts, IReadOnlyDictionary<string, JsonElement> contents)
    {
        var found = new Dictionary<FactKey, JsonElement>();
        foreach (var (key, value) in facts)
        {
            if (!contents.TryGetValue(value.Ref, out var content))
            {
                return KernelErrors.Invalid.With($"no stored content for {key.Value} ({value.Ref}) (fix: record while the instance's facts are kept)");
            }

            found[key] = content;
        }

        return found;
    }
}
