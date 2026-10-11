using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// 'workflow answer' (spec §5.8, §3.8, W7): the answer must fit the suspension (FrameRules.Fits), a grant is applied
/// to the parked instance (LoopRules.Grant, LoopRules.AnswerText, BudgetRules.GrantTokens), the frame is popped under
/// the profile's timings, and its resume action runs under the answer's own key. A pending park takes effect instead.
/// </summary>
public static partial class TransitionDecision
{
    private static readonly FactKey _answer = FactKey.From("fact:answer");

    private static Decision Answer(Deciding d, WorkflowInstance instance, AnswerGiven answer)
    {
        var fit = FrameRules.Fits(instance, d.Recipe, answer);
        if (fit.IsError)
        {
            return new Rejected(d.Key, fit.Error);
        }

        if (fit.Value.Kind == ResumeKind.Forward)
        {
            return new Absorbed(d.Key, $"the answer goes to the child the park mirrors, {instance.Suspended[^1].Suspension.Mirrors?.Value} (spec 5.7)");
        }

        var granted = Grant(d, instance, answer);
        if (granted.IsError)
        {
            return new Rejected(d.Key, granted.Error);
        }

        var popping = d.Rules.Pop(WithAnswer(granted.Value, answer), d.Recipe, child: null, d.Context);
        if (popping.IsError)
        {
            return new Rejected(d.Key, popping.Error);
        }

        var popped = popping.Value;
        return popped.Pending is { } pending
            ? d.Commit(d.Rules.Suspend(popped.Instance, new SuspendOutcome(pending.Suspension, pending.Resume), d.Now))
            : Recorded(Resume(d, popped.Instance, fit.Value), answer, fit.Value);
    }

    /// <summary>The answer's own event carries the grant or the override it made (§5.2).</summary>
    private static Decision Recorded(Decision decision, AnswerGiven answer, ResumeAction resume)
    {
        var grant = answer.Variant switch
        {
            AnswerVariant.Grant => new GrantRecord(answer.Amount, Tokens: null),
            AnswerVariant.GrantTokens => new GrantRecord(Rounds: null, answer.Amount),
            AnswerVariant.Text or AnswerVariant.Accept or AnswerVariant.Reject or AnswerVariant.Resume or _ => null,
        };
        var overridden = answer.Variant == AnswerVariant.Accept && resume.Gate is { } gate ? new OverrideRecord(gate) : null;
        if (grant is null && overridden is null)
        {
            return decision;
        }

        IReadOnlyList<InstanceChange> Mark(IReadOnlyList<InstanceChange> changes) =>
            changes.Count == 0 ? changes : [changes[0] with { Event = changes[0].Event with { Grant = grant, Override = overridden } }, .. changes.Skip(1)];

        return decision switch
        {
            Applied applied => applied with { Changes = Mark(applied.Changes) },
            Refused refused => refused with { Changes = Mark(refused.Changes) },
            _ => decision,
        };
    }

    /// <summary>A grant raises the loop's or the budget's allowance on the parked instance, before the pop (W7); '--text' on no-progress works once per loop.</summary>
    private static Result<WorkflowInstance> Grant(Deciding d, WorkflowInstance instance, AnswerGiven answer) => answer.Variant switch
    {
        AnswerVariant.Grant => LoopRules.Grant(instance, d.Recipe, answer.Amount ?? 0),
        AnswerVariant.GrantTokens => BudgetRules.GrantTokens(instance, d.Recipe, answer.Amount ?? 0),
        AnswerVariant.Text when instance.Park == ParkReason.NoProgress => LoopRules.AnswerText(instance, d.Recipe),
        AnswerVariant.Text or AnswerVariant.Accept or AnswerVariant.Reject or AnswerVariant.Resume or _ => instance,
    };

    /// <summary>'fact:answer' holds the latest answer text (§5.8), as a reference to its digest.</summary>
    private static WorkflowInstance WithAnswer(WorkflowInstance instance, AnswerGiven answer)
    {
        if (answer.Text is not { } text)
        {
            return instance;
        }

        var digest = RefusalRules.Digest(text);
        return instance with { Facts = new Dictionary<FactKey, FactValue>(instance.Facts) { [_answer] = new FactValue("blob:" + digest, digest, HeadSha: null) } };
    }

    /// <summary>The popped frame's resume action (§5.8 table).</summary>
    private static Decision Resume(Deciding d, WorkflowInstance instance, ResumeAction resume)
    {
        var withheld = resume.Withheld;
        return resume.Kind switch
        {
            ResumeKind.Transition when withheld?.Transition is { } index => Apply(d.Withholding(withheld.Trigger), instance, index, Records.None),
            ResumeKind.Reevaluate or ResumeKind.Override or ResumeKind.Refuse when withheld?.Transition is { } index =>
                Reevaluate(d.Withholding(withheld.Trigger), instance, index, resume),
            ResumeKind.Rerun => RerunAfterAnswer(d, instance, countsRound: true),
            ResumeKind.Restart when instance.Frame.Run is not null => RerunAfterAnswer(d, instance, countsRound: false),
            ResumeKind.None => new Rejected(d.Key, WorkflowErrors.TriggerNotAllowed.With(
                $"nothing resumes {instance.Address.Value} (fix: cancel it with sindri workflow cancel {instance.Address.Value})")),
            ResumeKind.Reemit => throw new NotSupportedException("A reemit resume is decided by the outbox rules (P6)."),
            ResumeKind.Transition or ResumeKind.Reevaluate or ResumeKind.Override or ResumeKind.Refuse or ResumeKind.Rearm
                or ResumeKind.Rewait or ResumeKind.Restart or ResumeKind.Return or ResumeKind.Forward or _ => d.Commit(instance),
        };
    }

    /// <summary>The withheld transition again: every gate and evidence check from the answer's gate answers; '--accept' overrides the escalated gate, '--reject' lets its refusal stand.</summary>
    private static Decision Reevaluate(Deciding d, WorkflowInstance instance, int index, ResumeAction resume)
    {
        var records = Check(d, d.Recipe.Transitions[index]);
        if (resume.Gate is { } gate && resume.Kind is ResumeKind.Override or ResumeKind.Refuse)
        {
            var outcome = resume.Kind == ResumeKind.Override ? GateOutcome.Overridden : GateOutcome.Refused;
            records = records with { Gates = [.. records.Gates.Select(g => g.Gate == gate ? g with { Outcome = outcome } : g)] };
        }

        return records.Refused ? Refuse(d, instance, index, records) : Apply(d, instance, index, records);
    }

    /// <summary>A new run of the Step in the same entry (§5.8 rerun, restart); a rerun counts a round of the state's loop.</summary>
    private static Applied RerunAfterAnswer(Deciding d, WorkflowInstance instance, bool countsRound)
    {
        var state = d.Recipe.States[instance.State];
        if (instance.Frame.Run is not { } run || state.Step is not { } step)
        {
            return d.Commit(instance);
        }

        var next = run with { Token = RunToken.For(run.Token.Entry, run.Token.Number + 1), Launch = null };
        var loops = countsRound && state.Loop is { } name && instance.Loops.TryGetValue(name, out var loop)
            ? With(instance.Loops, name, loop with { Round = loop.Round + 1 })
            : instance.Loops;
        var after = instance with { Frame = instance.Frame with { Run = next }, Loops = loops };
        var applied = d.Commit(after);
        return applied with { FollowUps = [new FollowUp(FollowUpKind.StartRun, instance.Id, next.Token, step.Use, Answer: null)] };
    }

    private sealed partial record Deciding
    {
        /// <summary>The answer a resume commits under, recorded as the event's trigger while the withheld trigger is replayed.</summary>
        public AnswerGiven? Answered { get; init; }

        /// <summary>This decision replaying <paramref name="withheld"/> under the answer's key.</summary>
        public Deciding Withholding(Trigger withheld) => this with
        {
            Input = Input with { Trigger = withheld },
            Answered = Answered ?? Input.Trigger as AnswerGiven,
        };
    }
}
