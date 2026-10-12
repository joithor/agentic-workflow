using System.Globalization;
using Aw.Sindri.Core.Events;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The desk runtime of <see cref="RecipeWorkflow"/> (engine spec rev 8.1 §5.3, W20; engine P4 unit (c)): 'RunStep'
/// records the open task, the workflow waits for the 'complete' Update bounded by the desk timeout, the validator
/// refuses a stale run token or a malformed report without touching state or history, and the timeout parks.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    /// <summary>
    /// 'workflow complete' from the desk (spec rev 8.1 §5.3): the report for the open run, which the desk wait consumes.
    /// Update id: the nonce. Unit (c).
    /// </summary>
    /// <param name="report">The report: the run token, a declared outcome and the stored outputs.</param>
    /// <returns>The instance after the report, and what happened ('-> checks').</returns>
    [WorkflowUpdate("complete")]
    public async Task<CommandResult> CompleteAsync(StepCompleted report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var id = Workflow.CurrentUpdateInfo?.Id ?? throw new InvalidOperationException("'complete' runs only as an Update");
        _report = report;
        _reportId = id;
        await Workflow.WaitConditionAsync(() => _reported.ContainsKey(id)).ConfigureAwait(true);
        _ = _reported.Remove(id, out var reported);
        return reported?.Result ?? throw Refusal(reported?.Refusal ?? WorkflowErrors.TriggerNotAllowed.With("the report was dropped"));
    }

    /// <summary>
    /// Rejects, recording nothing and consuming no round (rev 8.1 §5.3, W12): a run token that isn't the open run's, or a
    /// run already reported and being applied (SND-WF-005); a malformed report (SND-WF-008); a report the current state
    /// doesn't take (SND-WF-003). It runs the same pure decision the loop runs, so what it accepts the loop applies. Unit (c).
    /// </summary>
    /// <param name="report">The report.</param>
    [WorkflowUpdateValidator(nameof(CompleteAsync))]
    public void ValidateComplete(StepCompleted report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (_instance is not { } instance || _context is not { } context)
        {
            throw Refusal(WorkflowErrors.TriggerNotAllowed.With($"{_start?.WorkflowId().Value} is still starting (fix: run status, then complete the run it shows)"));
        }

        if (_report is not null)
        {
            throw Refusal(WorkflowErrors.StaleRunToken.With($"run {_report.Run.Value} was already reported and is being applied (fix: use the token in status)"));
        }

        if (TransitionDecision.Decide(new DecisionInput(instance, context.Recipe, report, GateAnswers.None, context.Decisions)) is Rejected rejected)
        {
            throw Refusal(rejected.Error);
        }
    }

    /// <summary>
    /// One pass over an active state's desk Step: wait for its report or its timeout, decide, mirror a change into the
    /// ledger, and hand the outcome to the 'complete' Update that carried the report.
    /// </summary>
    private async Task<WorkflowInstance> RunDeskStep(WorkflowInstance instance, OpenRun open, InstanceStart start, Interpreting context)
    {
        if (await RunStep(instance.Frame, open, context.Recipe.States[instance.Frame.State]).ConfigureAwait(true) is not { } trigger)
        {
            return await ApplySubmitted(instance, open, context).ConfigureAwait(true);
        }

        var reportId = _reportId;
        _reportId = null;
        var before = instance;
        var decision = trigger is DeadlineFired
            ? null
            : TransitionDecision.Decide(new DecisionInput(instance, context.Recipe, trigger, GateAnswers.None, context.Decisions));
        if (decision is Rejected rejected)
        {
            Settle(reportId, new Reported(Result: null, rejected.Error));   // the validator saw the same instance, so this is a bug guard
            return instance;
        }

        instance = decision is null ? DeskTimeouts.Expire(instance, _waited, Timers) : After(instance, decision);
        if (!ReferenceEquals(before, instance))
        {
            if (instance.Frame.Run?.Token != before.Frame.Run?.Token)
            {
                _timeoutAt = null;   // a new run arms its own deadline in RunStep
            }

            Enter(instance);
            await Mirror(Transition(start, before, instance, trigger, open), NeedsYou(instance)).ConfigureAwait(true);
        }

        Settle(reportId, new Reported(new CommandResult(GetInstance(), Happened(before, instance, decision)), Refusal: null));
        return instance;
    }

    /// <summary>
    /// Applies an answer, command or cancel another Update submitted while the desk task is open (unit (a)'s
    /// <see cref="Submit"/>); a change that ends the run clears its deadline, so the next run arms its own.
    /// </summary>
    private async Task<WorkflowInstance> ApplySubmitted(WorkflowInstance instance, OpenRun open, Interpreting context)
    {
        var after = await Apply(instance, _submitted.Dequeue(), context).ConfigureAwait(true);
        if (after.Frame.Run?.Token != open.Token)
        {
            _timeoutAt = null;
        }

        return after;
    }

    /// <summary>The 'workflow.transition' row for a desk report or the desk timeout.</summary>
    private static WorkflowEventRecord Transition(InstanceStart start, WorkflowInstance before, WorkflowInstance after, Trigger trigger, OpenRun open)
    {
        var (kind, outcome) = trigger switch
        {
            StepCompleted c => (TriggerNames.StepCompleted, c.Outcome.Value),
            StepFailed => (TriggerNames.StepFailed, null),
            _ => (TriggerNames.Deadline, null),
        };
        return new WorkflowEventRecord(start.WorkflowId().Value, EventTypes.WorkflowTransition, start.Item.Value, before.State, after.State, kind, outcome, open.Token.Value);
    }

    /// <summary>
    /// Runs one desk Step (rev 8.1 §5.3): once per run, the short 'RunStep' activity with the run token in its id
    /// ('run:{token}:{n}') records the open task, and the wait's deadline is armed when none is (a new run, or an answer that re-arms it): the state's timeout raised to
    /// 'workflow.deskTimeout' (W20). The workflow then waits for the 'complete' Update's report; its expiry is the desk
    /// timeout, which always parks (W20, §3.8). A later wait on the same run keeps its deadline. Unit (c). The wait also
    /// wakes for an answer, command or cancel another Update submitted; it then returns null and the loop applies that
    /// command (unit (b)).
    /// </summary>
    private async Task<Trigger?> RunStep(Frame frame, OpenRun open, RecipeState state)
    {
        if (!string.Equals(_recorded, open.Token.Value, StringComparison.Ordinal))
        {
            _runs++;
            var step = new StepRun(_start!.WorkflowId().Value, frame.State, open.Token.Value); // why: RunStep runs only after RunAsync set _start
            await Workflow.ExecuteActivityAsync(
                (RunStepActivities a) => a.RunStepAsync(step),
                new ActivityOptions
                {
                    ActivityId = string.Create(CultureInfo.InvariantCulture, $"run:{open.Token.Value}:{_runs}"),
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                }).ConfigureAwait(true);
            _recorded = open.Token.Value;
            _timeoutAt = null;
        }

        var left = Arm(open, state) - Now;
        var woke = left > TimeSpan.Zero && await Workflow.WaitConditionAsync(() => _report is not null || _submitted.Count > 0, left).ConfigureAwait(true);
        if (woke && _report is { } report)
        {
            _report = null;
            return report;
        }

        if (woke)
        {
            return null;   // a submitted command; the deadline stays armed for this run
        }

        _timeoutAt = null;
        return new DeadlineFired(Timers, frame.Entry, DeadlineKind.State);
    }

    /// <summary>
    /// The run's deadline: armed when none is (a new run, or an answer that re-arms it) to the state's timeout, raised to
    /// 'workflow.deskTimeout' for a desk Step (W20); a later wait on the same run keeps it.
    /// </summary>
    private DateTimeOffset Arm(OpenRun open, RecipeState state)
    {
        if (_timeoutAt is { } armed)
        {
            return armed;
        }

        _waited = open.Runtime == StepRuntime.Desk ? DeskTimeouts.Wait(state.Timeout, _deskTimeout) : state.Timeout?.After.Span ?? _deskTimeout;
        _timeoutAt = Now + _waited;
        return _timeoutAt.Value;
    }

    /// <summary>Hands what the loop did with a report to the 'complete' Update that carried it.</summary>
    private void Settle(string? reportId, Reported reported)
    {
        if (reportId is not null)
        {
            _reported[reportId] = reported;
        }
    }

    /// <summary>What a report did, in the words the CLI prints (spec §6.3): '-> checks', 'refused: re-running produce (round 2/3)', 'parked: rounds'.</summary>
    private static string Happened(WorkflowInstance before, WorkflowInstance after, Decision? decision) => (decision, after) switch
    {
        (_, { Suspension: { Kind: SuspensionKind.Park, Reason: { } reason } }) => "parked: " + ReasonName(reason),
        (_, { Suspension.Kind: SuspensionKind.Escalation }) => "escalated",
        (Refused, _) when after.Frame.Run is { } rerun => $"refused: re-running {after.Frame.State} (run {rerun.Token.Value})",
        (Refused, _) => "refused",
        (Absorbed absorbed, _) => "nothing changed: " + absorbed.Reason,
        _ when ReferenceEquals(before, after) => "nothing changed",
        _ => "-> " + after.State,
    };

    private static string ReasonName(ParkReason reason) => reason switch
    {
        ParkReason.Rounds => "rounds",
        ParkReason.NoProgress => "no-progress",
        ParkReason.Budget => "budget",
        ParkReason.NeedsAnswer => "needs-answer",
        ParkReason.Timeout => "timeout",
        ParkReason.SpecConflict => "spec-conflict",
        ParkReason.NoRuntime or _ => "SND-WF-010",
    };

    /// <summary>A validator's refusal, or a report the loop rejected: the ApplicationFailure the client maps back to its code.</summary>
    private static ApplicationFailureException Refusal(Error error) =>
        new($"{error.Message}", errorType: error.Code, nonRetryable: true);
}
