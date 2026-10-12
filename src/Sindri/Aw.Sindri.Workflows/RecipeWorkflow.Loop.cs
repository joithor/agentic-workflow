using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The seam between the Updates and the interpreter's loop (engine spec rev 8 §5.2, §5.8). <b>Unit (a) owns this
/// file</b> ('origin/feat/sindri-wf-p4-a'); this is unit (b)'s minimal stand-in with (a)'s names and signatures, so the
/// answer, command and cancel Updates run end to end before (a) lands, and (a)'s version replaces it whole: an Update
/// hands its trigger to the loop through <see cref="Submit"/>; a parked, escalated or run-less instance waits for one
/// (<see cref="Wait"/>); the loop decides it with P3's pure <see cref="TransitionDecision"/>, mirrors a change into the
/// ledger, and hands the decision back. Delivered events at a wait are (a)'s.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private readonly Queue<Submission> _submitted = new();
    private readonly Dictionary<int, Decision> _decided = [];
    private int _tickets;

    /// <summary>
    /// The seam the Updates call (units (b), (c)): hands <paramref name="trigger"/> to the loop, which decides it at the
    /// instance's current wait, applies and mirrors it, and returns the decision. Its validator has already run.
    /// </summary>
    /// <param name="trigger">The command's trigger (an answer, a command, a cancel …).</param>
    /// <returns>The decision: applied, refused, absorbed, or rejected (nothing recorded).</returns>
    private async Task<Decision> Submit(Trigger trigger)
    {
        var ticket = ++_tickets;
        _submitted.Enqueue(new Submission(ticket, trigger));
        await Workflow.WaitConditionAsync(() => _decided.ContainsKey(ticket)).ConfigureAwait(true);
        _ = _decided.Remove(ticket, out var decision);
        return decision ?? throw new InvalidOperationException("the loop settled the ticket without a decision");
    }

    /// <summary>Waits for the next submitted command and decides it (a park, an escalation, or a state with no open run).</summary>
    private async Task<WorkflowInstance> Wait(WorkflowInstance instance, Interpreting context)
    {
        await Workflow.WaitConditionAsync(() => _submitted.Count > 0).ConfigureAwait(true);
        return await Apply(instance, _submitted.Dequeue(), context).ConfigureAwait(true);
    }

    /// <summary>
    /// Decides one trigger and applies its change (§5.1, §5.6): the instance after it, one ledger event through
    /// 'RecordEvent', the search attributes; the decision goes back to the Update that submitted it.
    /// </summary>
    private async Task<WorkflowInstance> Apply(WorkflowInstance instance, Submission submission, Interpreting context)
    {
        var decision = TransitionDecision.Decide(new DecisionInput(instance, context.Recipe, submission.Trigger, GateAnswers.None, context.Decisions));
        var after = After(instance, decision);
        if (OwnChange(instance, decision) is { } change)
        {
            Enter(after);
            await Mirror(Record(instance, change, submission.Trigger), NeedsYou(after)).ConfigureAwait(true);
        }

        if (submission.Ticket != 0)
        {
            _decided[submission.Ticket] = decision;
        }

        return after;
    }

    /// <summary>The decision's change to this instance (an applied or refused trigger); none for a rejected or absorbed one.</summary>
    private static InstanceChange? OwnChange(WorkflowInstance instance, Decision decision) => decision switch
    {
        Applied applied => applied.Changes.FirstOrDefault(c => c.After.Id == instance.Id),
        Refused refused => refused.Changes.FirstOrDefault(c => c.After.Id == instance.Id),
        _ => null,
    };

    /// <summary>The ledger row for <paramref name="change"/>: its event type, the states, the trigger and the run it was on.</summary>
    private WorkflowEventRecord Record(WorkflowInstance before, InstanceChange change, Trigger trigger) => new(
        _start!.WorkflowId().Value, // why: the loop runs only after RunAsync set _start
        TypeOf(change.Event.Type),
        _start.Item.Value,
        before.State,
        change.After.State,
        trigger.Name,
        trigger is StepCompleted completed ? completed.Outcome.Value : null,
        before.Frame.Run?.Token.Value);

    private static string TypeOf(WorkflowEventType type) => type switch
    {
        WorkflowEventType.Started => "workflow.started",
        WorkflowEventType.Refused => "workflow.refused",
        WorkflowEventType.Shifted => "workflow.shifted",
        WorkflowEventType.Notified => "workflow.notified",
        WorkflowEventType.Expired => "workflow.expired",
        WorkflowEventType.EffectRefused => "workflow.effect-refused",
        WorkflowEventType.Transition or _ => "workflow.transition",
    };
}
