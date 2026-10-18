using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// 'recipe eval --record' from the ledger (spec §6.3, §9.2; engine P7, owner answer Q6): a served instance's events written back as an
/// eval case. A line is built from what a ledger row shows (the trigger's kind and outcome, the gates' verdicts, the delta's state, park
/// and outcome, and the facts' content from the stored facts), so a child's park, resume and escalation come back as 'child.parked',
/// 'child.resumed' and 'child.escalated', and its end as a 'wait.satisfied' with the child's declared outputs the parent kept under
/// the child's own keys. A trigger an eval line can't name (a held event's expiry, a drift wake) refuses the recording.
/// </summary>
public static class EvalLedgerRecorder
{
    /// <summary>The case that replays <paramref name="events"/>.</summary>
    /// <param name="name">The case's name.</param>
    /// <param name="recipe">The instance's recipe, which says which state waits on a child.</param>
    /// <param name="children">The pinned child recipes, whose declared outputs a child's end may carry.</param>
    /// <param name="events">The instance's 'workflow.*' ledger payloads, oldest first, from its start.</param>
    /// <param name="contents">The stored fact content, by SHA-256.</param>
    /// <returns>The case, or why the events can't be one.</returns>
    public static Result<EvalCase> Record(
        string name,
        Recipe recipe,
        IChildRecipeResolver children,
        IReadOnlyList<WorkflowEventPayload> events,
        IReadOnlyDictionary<string, JsonElement> contents)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(contents);
        if (events.Count == 0 || events[0].Detail is not { } started || !string.Equals(events[0].Trigger, "start", StringComparison.Ordinal))
        {
            return KernelErrors.Invalid.With("the events don't start with workflow.started (fix: record from the instance's first event)");
        }

        var inputs = Facts(started.Delta.Facts, contents);
        if (inputs.IsError)
        {
            return inputs.Error;
        }

        var steps = Steps(recipe, children, events, started.Delta.Frame?.State ?? recipe.Initial, contents);
        return steps.IsError ? steps.Error : new EvalCase(name, new EvalStartLine(new EvalStart(inputs.Value), Scaffold: null), steps.Value);
    }

    private static Result<List<EvalStepLine>> Steps(Recipe recipe, IChildRecipeResolver children, IReadOnlyList<WorkflowEventPayload> events, string state, IReadOnlyDictionary<string, JsonElement> contents)
    {
        var steps = new List<EvalStepLine>(events.Count - 1);
        for (var i = 1; i < events.Count; i++)
        {
            var step = Step(events[i], i + 1, recipe.States.GetValueOrDefault(state), children, contents);
            if (step.IsError)
            {
                return step.Error;
            }

            steps.Add(step.Value);
            state = events[i].Detail?.Delta.Frame?.State ?? state;
        }

        return steps;
    }

    private static Result<EvalStepLine> Step(WorkflowEventPayload row, int number, RecipeState? at, IChildRecipeResolver children, IReadOnlyDictionary<string, JsonElement> contents)
    {
        if (row.Detail is not { } detail)
        {
            return KernelErrors.Invalid.With($"event {Text(number)} ({row.Trigger}) carries no detail, so it can't be an eval line (fix: record a history without it)");
        }

        var facts = Facts(detail.Delta.Facts, contents);
        if (facts.IsError)
        {
            return facts.Error;
        }

        var trigger = Trigger(row, detail, Outputs(row, at, children, facts.Value), number);
        if (trigger.IsError)
        {
            return trigger.Error;
        }

        var suspension = detail.Delta.Suspension;
        var park = suspension is { Kind: SuspensionKind.Park } ? suspension.Reason : null;
        var outcome = detail.Delta.Outcome is { } ended ? OutcomeName.From(ended) : (OutcomeName?)null;
        var gates = detail.Gates.Where(static g => g.Verdict is not null).ToDictionary(static g => g.Gate, static g => g.Verdict!.Value); // why: the Where keeps answered gates
        return new EvalStepLine(trigger.Value, new EvalExpectation(row.To, park, outcome), gates.Count == 0 ? null : gates);
    }

    /// <summary>What the line's trigger reported: a step's facts as the delta saved them, a child's end only its declared outputs under the child's own keys.</summary>
    private static Dictionary<FactKey, JsonElement>? Outputs(WorkflowEventPayload row, RecipeState? at, IChildRecipeResolver children, Dictionary<FactKey, JsonElement> facts)
    {
        var reported = string.Equals(row.Trigger, TriggerNames.WaitSatisfied, StringComparison.Ordinal) && at?.WaitFor?.Child is { } wait
            ? Declared(children, wait, facts)
            : facts;
        return reported.Count == 0 ? null : reported;
    }

    private static Dictionary<FactKey, JsonElement> Declared(IChildRecipeResolver children, ChildWait wait, Dictionary<FactKey, JsonElement> facts)
    {
        var declared = children.Resolve(wait.Recipe) is { } found ? ChildOutputs.Declared(found.Recipe) : [];
        return facts.Where(f => declared.Contains(f.Key)).ToDictionary();
    }

    private static Result<EvalTrigger> Trigger(WorkflowEventPayload row, WorkflowEventDetail detail, Dictionary<FactKey, JsonElement>? outputs, int number)
    {
        var kind = row.Trigger;
        var outcome = row.Outcome is { } named ? OutcomeName.From(named) : (OutcomeName?)null;
        var suspension = detail.Delta.Suspension;
        return kind switch
        {
            TriggerNames.StepCompleted or TriggerNames.WaitSatisfied => new EvalTrigger(kind, outcome, outputs, Park: null),
            TriggerNames.StepFailed => new EvalTrigger(kind, Outcome: null, outputs, Park: null, Reason: detail.Reason),
            TriggerNames.ChildParked => new EvalTrigger(kind, Outcome: null, Outputs: null, suspension is { Kind: SuspensionKind.Park } ? suspension.Reason : null),
            TriggerNames.ChildResumed or TriggerNames.ChildEscalated => new EvalTrigger(kind, Outcome: null, Outputs: null, Park: null),
            TriggerNames.Answer when detail.Answer is { } answer => new EvalTrigger(kind, Outcome: null, outputs, Park: null, Variant(answer), detail.Amount),
            _ when EvalTriggerRules.For(kind) is { } variant => new EvalTrigger(kind, Outcome: null, Outputs: null, Park: null, variant),
            _ => KernelErrors.Invalid.With($"{kind} at event {Text(number)} is a trigger an eval line can't name (fix: record a history without it)"),
        };
    }

    /// <summary>A stored fact's content as an eval line spells it: its text.</summary>
    /// <param name="content">The stored bytes.</param>
    /// <returns>The content as a JSON string.</returns>
    public static JsonElement ContentOf(ReadOnlySpan<byte> content) => EvalFacts.OfText(System.Text.Encoding.UTF8.GetString(content));

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static EvalVariant Variant(AnswerVariant answer) => answer switch
    {
        AnswerVariant.Text => EvalVariant.Text,
        AnswerVariant.Grant => EvalVariant.Grant,
        AnswerVariant.GrantTokens => EvalVariant.GrantTokens,
        AnswerVariant.Accept => EvalVariant.Accept,
        AnswerVariant.Reject => EvalVariant.Reject,
        AnswerVariant.Resume or _ => EvalVariant.Resume,
    };

    private static Result<Dictionary<FactKey, JsonElement>> Facts(IReadOnlyDictionary<string, string>? shas, IReadOnlyDictionary<string, JsonElement> contents)
    {
        var found = new Dictionary<FactKey, JsonElement>();
        foreach (var (key, sha) in shas ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            if (!contents.TryGetValue(sha, out var content))
            {
                return KernelErrors.Invalid.With($"no stored content for {key} (sha256 {sha}) (fix: record while the instance's facts are kept)");
            }

            found[FactKey.From(key)] = content;
        }

        return found;
    }
}
