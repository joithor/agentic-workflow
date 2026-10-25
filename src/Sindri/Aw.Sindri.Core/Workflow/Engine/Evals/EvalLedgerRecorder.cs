using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Blocks;
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
    private static readonly Dictionary<string, string> _noFacts = new(StringComparer.Ordinal);

    /// <summary>The case that replays <paramref name="events"/>.</summary>
    /// <param name="name">The case's name.</param>
    /// <param name="pinned">What the events ran against: the recipe, its pinned children and the build's descriptors.</param>
    /// <param name="events">The instance's 'workflow.*' ledger payloads, oldest first, from its start.</param>
    /// <param name="contents">The stored fact content, by SHA-256.</param>
    /// <returns>The case, or why the events can't be one.</returns>
    public static Result<EvalCase> Record(
        string name,
        LedgerRecording pinned,
        IReadOnlyList<WorkflowEventPayload> events,
        IReadOnlyDictionary<string, JsonElement> contents)
    {
        ArgumentNullException.ThrowIfNull(pinned);
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

        var replay = new Replay(pinned, contents, new Dictionary<string, string>(started.Delta.Facts ?? _noFacts, StringComparer.Ordinal));
        var steps = Steps(replay, events, started.Delta.Frame?.State ?? pinned.Recipe.Initial);
        return steps.IsError ? steps.Error : new EvalCase(name, new EvalStartLine(new EvalStart(inputs.Value), Scaffold: null), steps.Value);
    }

    /// <summary>What a recording reads while it walks the events: what they ran against, the stored content, and the facts the instance kept so far (by SHA-256).</summary>
    private sealed record Replay(LedgerRecording Pinned, IReadOnlyDictionary<string, JsonElement> Contents, Dictionary<string, string> Kept);

    private static Result<List<EvalStepLine>> Steps(Replay replay, IReadOnlyList<WorkflowEventPayload> events, string state)
    {
        var steps = new List<EvalStepLine>(events.Count - 1);
        for (var i = 1; i < events.Count; i++)
        {
            foreach (var (key, sha) in events[i].Detail?.Delta.Facts ?? _noFacts)
            {
                replay.Kept[key] = sha;
            }

            var step = Step(events[i], i + 1, replay.Pinned.Recipe.States.GetValueOrDefault(state), replay);
            if (step.IsError)
            {
                return step.Error;
            }

            steps.Add(step.Value);
            state = events[i].Detail?.Delta.Frame?.State ?? state;
        }

        return steps;
    }

    private static Result<EvalStepLine> Step(WorkflowEventPayload row, int number, RecipeState? at, Replay replay)
    {
        if (row.Detail is not { } detail)
        {
            return KernelErrors.Invalid.With($"event {Text(number)} ({row.Trigger}) carries no detail, so it can't be an eval line (fix: record a history without it)");
        }

        var facts = Facts(detail.Delta.Facts, replay.Contents);
        if (facts.IsError)
        {
            return facts.Error;
        }

        var carried = Carried(row, at, replay, Outputs(row, at, replay.Pinned.Children, facts.Value));
        if (carried.IsError)
        {
            return carried.Error;
        }

        var trigger = Trigger(row, detail, carried.Value, number);
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

    /// <summary>
    /// A completed Step's report with the outputs its descriptor requires on the outcome: a delta holds only the facts a report added or
    /// changed, so one reported again unchanged (the answer log of a round that added nothing, the map an apply left as it was) is
    /// missing from it, and the line takes it from the facts the instance kept at that point.
    /// </summary>
    private static Result<Dictionary<FactKey, JsonElement>?> Carried(WorkflowEventPayload row, RecipeState? at, Replay replay, Dictionary<FactKey, JsonElement>? reported)
    {
        if (Reporting(row, at, replay.Pinned.Catalog) is not var (descriptor, outcome))
        {
            return reported;
        }

        var outputs = reported ?? [];
        foreach (var spec in descriptor.Outputs.Where(o => StepOutputs.CallerWritesOn(o, outcome) && !outputs.ContainsKey(o.Fact) && replay.Kept.ContainsKey(o.Fact.Value))) // why: a fact never written stays out, so the replay reports the same gap the run had
        {
            var sha = replay.Kept[spec.Fact.Value];
            if (!replay.Contents.TryGetValue(sha, out var content))
            {
                return KernelErrors.Invalid.With($"no stored content for {spec.Fact.Value} (sha256 {sha}) (fix: record while the instance's facts are kept)");
            }

            outputs[spec.Fact] = content;
        }

        return outputs.Count == 0 ? null : outputs;
    }

    /// <summary>The descriptor of the Step a completed report is on, and the outcome it reports; none for any other row.</summary>
    private static (BlockDescriptor Descriptor, OutcomeName Outcome)? Reporting(WorkflowEventPayload row, RecipeState? at, BlockCatalog catalog) =>
        string.Equals(row.Trigger, TriggerNames.StepCompleted, StringComparison.Ordinal) && row.Outcome is { } named && at?.Step is { } step && catalog.Find(step.Use) is { } descriptor
            ? (descriptor, OutcomeName.From(named))
            : null;

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

    /// <summary>A stored fact's content as an eval line spells it: JSON text of an object or array as that JSON value (the schemas and the recipe's readers see the value), any other text as a JSON string.</summary>
    /// <param name="content">The stored bytes.</param>
    /// <returns>The content as a JSON value.</returns>
    public static JsonElement ContentOf(ReadOnlySpan<byte> content)
    {
        var text = System.Text.Encoding.UTF8.GetString(content);
        return AsJson(text) ?? EvalFacts.OfText(text);
    }

    private static JsonElement? AsJson(string text)
    {
        if (text.AsSpan().TrimStart() is not ['{' or '[', ..])
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null; // why: text that only starts like JSON is still text
        }
    }

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
