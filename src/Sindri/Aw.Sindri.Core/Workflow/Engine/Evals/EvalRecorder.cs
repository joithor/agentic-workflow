using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// 'recipe eval --record' (spec §6.3, §9.2): a live instance's history written as an eval case. The events give the
/// triggers, gate verdicts and post-states (folded with <see cref="EventFold"/>); a line's outputs are the facts its
/// trigger reported, as the store saved them, and their content comes from the store's facts, never from the events,
/// which hold only references (an event's delta leaves out a fact reported again unchanged). The query against serve
/// lands with the store (P4).
/// </summary>
public static class EvalRecorder
{
    private static readonly FactKey _answer = FactKey.From("fact:answer");

    /// <summary>The case that replays <paramref name="events"/>.</summary>
    /// <param name="name">The case's name.</param>
    /// <param name="events">The instance's events, oldest first, from its workflow.started.</param>
    /// <param name="reported">The facts each trigger reported, by its key, as the store saved them; a trigger missing here gives the facts its event's delta added.</param>
    /// <param name="contents">The store's fact content, by <see cref="FactValue.Ref"/>.</param>
    /// <returns>The case, or why the history can't be one: a broken log, a missing fact's content, or a trigger an eval line can't name.</returns>
    public static Result<EvalCase> Record(
        string name,
        IReadOnlyList<WorkflowEvent> events,
        IReadOnlyDictionary<TriggerKey, IReadOnlyDictionary<FactKey, FactValue>> reported,
        IReadOnlyDictionary<string, JsonElement> contents)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(reported);
        ArgumentNullException.ThrowIfNull(contents);
        if (events.Count == 0 || events[0].Type != WorkflowEventType.Started)
        {
            return KernelErrors.Invalid.With("the history doesn't start with workflow.started (fix: record from the instance's first event)");
        }

        WorkflowInstance row;
        try
        {
            row = EventFold.Apply(before: null, events[0]);
        }
        catch (ArgumentException ex)
        {
            return KernelErrors.Invalid.With($"the history is broken: {ex.Message}");
        }

        var inputs = Contents(row.Facts, contents);
        if (inputs.IsError)
        {
            return inputs.Error;
        }

        var steps = new List<EvalStepLine>(events.Count - 1);
        for (var i = 1; i < events.Count; i++)
        {
            var step = Step(row, events[i], reported.GetValueOrDefault(events[i].Trigger.Key) ?? events[i].Delta.Facts, contents);
            if (step.IsError)
            {
                return step.Error;
            }

            (row, var line) = step.Value;
            steps.Add(line);
        }

        return new EvalCase(name, new EvalStartLine(new EvalStart(inputs.Value), Scaffold: null), steps);
    }

    private static Result<(WorkflowInstance Row, EvalStepLine Line)> Step(
        WorkflowInstance before, WorkflowEvent recorded, IReadOnlyDictionary<FactKey, FactValue>? reported, IReadOnlyDictionary<string, JsonElement> contents)
    {
        WorkflowInstance after;
        try
        {
            after = EventFold.Apply(before, recorded);
        }
        catch (ArgumentException ex)
        {
            return KernelErrors.Invalid.With($"the history is broken at version {recorded.ToVersion.ToString(CultureInfo.InvariantCulture)}: {ex.Message}");
        }

        var facts = Contents(reported ?? new Dictionary<FactKey, FactValue>(), contents);
        if (facts.IsError)
        {
            return facts.Error;
        }

        var trigger = Trigger(new Recorded(recorded, before, after), facts.Value, contents);
        if (trigger.IsError)
        {
            return trigger.Error;
        }

        var gates = recorded.Gates.Where(static g => g.Verdict is not null).ToDictionary(static g => g.Gate, static g => g.Verdict!.Value); // why: the Where keeps answered gates
        var expect = new EvalExpectation(after.State, after.Park, after.Outcome);
        return (after, new EvalStepLine(trigger.Value, expect, gates.Count == 0 ? null : gates));
    }

    private static Result<EvalTrigger> Trigger(Recorded step, Dictionary<FactKey, JsonElement> facts, IReadOnlyDictionary<string, JsonElement> contents)
    {
        var (recorded, _, after) = step;
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
            _ => KernelErrors.Invalid.With($"{kind} at version {recorded.ToVersion.ToString(CultureInfo.InvariantCulture)} is an engine wake an eval line can't name (fix: record a history without it)"),
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
        var (recorded, before, after) = step;
        var text = facts.TryGetValue(_answer, out var given) ? new Dictionary<FactKey, JsonElement> { [_answer] = given } : null;
        if (before.Suspended.Count == 0)
        {
            if (text is null && after.Facts.TryGetValue(_answer, out var kept))
            {
                if (!contents.TryGetValue(kept.Ref, out var content))
                {
                    return KernelErrors.Invalid.With($"the store has no content for {_answer.Value} ({kept.Ref}) (fix: record while the instance's facts are kept)");
                }

                text = new Dictionary<FactKey, JsonElement> { [_answer] = content };
            }

            return new EvalTrigger(TriggerNames.Answer, Outcome: null, text, Park: null, EvalVariant.Text);
        }

        var (variant, amount) = Fitted(before.Suspended[^1].Suspension, recorded) ?? recorded switch
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

    /// <summary>One event of the history with the rows before and after it.</summary>
    private sealed record Recorded(WorkflowEvent Event, WorkflowInstance Before, WorkflowInstance After);

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
                return KernelErrors.Invalid.With($"the store has no content for {key.Value} ({value.Ref}) (fix: record while the instance's facts are kept)");
            }

            found[key] = content;
        }

        return found;
    }
}
