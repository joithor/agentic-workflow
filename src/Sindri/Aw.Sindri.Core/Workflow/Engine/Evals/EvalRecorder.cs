using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
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
            TriggerNames.StepCompleted or TriggerNames.StepFailed or TriggerNames.WaitSatisfied =>
                new EvalTrigger(kind, recorded.Trigger.Outcome, Without(outputs, _answer), Park: null),
            TriggerNames.ChildParked => new EvalTrigger(kind, Outcome: null, Outputs: null, after.Park),
            TriggerNames.ChildResumed or TriggerNames.ChildEscalated => new EvalTrigger(kind, Outcome: null, Outputs: null, Park: null),
            TriggerNames.Answer => Answer(step, facts, contents),
            TriggerNames.Deadline or TriggerNames.TakeoverIdle => new EvalTrigger(TriggerNames.Deadline, Outcome: null, Outputs: null, Park: null, EvalVariant.Deadline),
            _ when EvalTriggerRules.For(kind) is { } variant => new EvalTrigger(kind, Outcome: null, Outputs: null, Park: null, variant),
            _ => KernelErrors.Invalid.With($"{kind} at event {Text(step.Number)} is an engine wake an eval line can't name (fix: record a history without it)"),
        };
    }

    /// <summary>
    /// An answer's option, from what it answered (the row before it) and what its event recorded: with nothing suspended,
    /// the answer to an answer wait ('text', its text from the row after it when the delta leaves out a repeated
    /// answer); on a needs-answer (wait), timeout or SND-WF-010 park, or a timeout escalation, 'resume' (with any text it
    /// gave); on a gate escalation, 'accept' when it overrode the gate and 'reject' when it was refused; otherwise a
    /// grant, an override, a refusal, the text it left, or a plain resume.
    /// </summary>
    private static Result<EvalTrigger> Answer(Recorded step, Dictionary<FactKey, JsonElement> facts, IReadOnlyDictionary<string, JsonElement> contents)
    {
        var (recorded, before, after, _) = step;
        var text = facts.TryGetValue(_answer, out var given) ? new Dictionary<FactKey, JsonElement> { [_answer] = given } : null;
        if (before.Suspension is null)
        {
            if (text is null && after.Facts.TryGetValue(_answer, out var kept))
            {
                if (!contents.TryGetValue(kept.Ref, out var content))
                {
                    return KernelErrors.Invalid.With($"no stored content for {_answer.Value} ({kept.Ref}) (fix: record while the instance's facts are kept)");
                }

                text = new Dictionary<FactKey, JsonElement> { [_answer] = content };
            }

            return new EvalTrigger(TriggerNames.Answer, Outcome: null, text, Park: null, EvalVariant.Text);
        }

        var (variant, amount) = Fitted(before.Suspension, recorded) ?? recorded switch
        {
            { Grant.Rounds: { } rounds } => (EvalVariant.Grant, rounds),
            { Grant.Tokens: { } tokens } => (EvalVariant.GrantTokens, tokens),
            { Override: not null } => (EvalVariant.Accept, null),
            { Type: WorkflowEventType.Refused } => (EvalVariant.Reject, null),
            _ when text is not null => (EvalVariant.Text, null),
            _ => (EvalVariant.Resume, null),
        };
        return new EvalTrigger(TriggerNames.Answer, Outcome: null, text, Park: null, variant, amount);
    }

    /// <summary>The option a suspension takes whatever its event shows: resume on a wait's needs-answer, a timeout or SND-WF-010; accept or reject on a gate escalation.</summary>
    private static (EvalVariant Variant, int? Amount)? Fitted(Suspension suspension, WorkflowEvent recorded) => suspension switch
    {
        { Reason: ParkReason.NeedsAnswer, ParkKind: ParkKind.Wait } or { Reason: ParkReason.Timeout or ParkReason.NoRuntime }
            or { Escalation: EscalationKind.Timeout } => (EvalVariant.Resume, null),
        { Escalation: EscalationKind.Gate } when recorded.Override is not null => (EvalVariant.Accept, null),
        { Escalation: EscalationKind.Gate } when recorded.Type == WorkflowEventType.Refused => (EvalVariant.Reject, null),
        _ => null,
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
