using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The desk runtime of <see cref="RecipeWorkflow"/> (engine spec rev 8.1 §5.3). <b>Unit (c) owns this file</b>; this is
/// unit (a)'s minimal stand-in so the state loop runs end to end before (c) lands, and (c)'s version replaces it whole:
/// the 'RunStep' activity once per run, then the loop's wait, where the 'complete' Update's report arrives through
/// <see cref="Submit"/>. Left to (c): the 'workflow.deskTimeout' floor and what the desk timeout does on expiry, the
/// report checks beyond the pure decision (SND-WF-005 while a report is being applied, SND-WF-008), and the CLI wording.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    /// <summary>'workflow complete' from the desk (spec rev 8.1 §5.3): the report for the open run. Update id: the nonce.</summary>
    /// <param name="report">The report: the run token, a declared outcome and the stored outputs.</param>
    /// <returns>The instance after the report, and what happened ('-> checks').</returns>
    [WorkflowUpdate("complete")]
    public async Task<CommandResult> CompleteAsync(StepCompleted report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var decision = await Submit(report).ConfigureAwait(true);
        return decision is Rejected rejected
            ? throw new ApplicationFailureException(rejected.Error.Message, errorType: rejected.Error.Code, nonRetryable: true)
            : new CommandResult(GetInstance(), "-> " + GetInstance().State);
    }

    /// <summary>Rejects, recording nothing, what the pure decision rejects: a stale run token (SND-WF-005), a report the state doesn't take.</summary>
    /// <param name="report">The report.</param>
    [WorkflowUpdateValidator(nameof(CompleteAsync))]
    public void ValidateComplete(StepCompleted report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (_instance is not { } instance || _context is not { } context)
        {
            throw new ApplicationFailureException("the instance is still starting (fix: run status, then complete the run it shows)", nonRetryable: true);
        }

        if (TransitionDecision.Decide(new DecisionInput(instance, context.Recipe, report, GateAnswers.None, context.Decisions)) is Rejected rejected)
        {
            throw new ApplicationFailureException(rejected.Error.Message, errorType: rejected.Error.Code, nonRetryable: true);
        }
    }

    /// <summary>
    /// One pass over an active state's desk Step: the short 'RunStep' activity, once per run, with the run token in its id
    /// ('run:{token}:{n}'), then the loop's wait for the report (or any other command).
    /// </summary>
    private async Task<WorkflowInstance> RunDeskStep(WorkflowInstance instance, OpenRun open, InstanceStart start, Interpreting context)
    {
        if (!string.Equals(_recorded, open.Token.Value, StringComparison.Ordinal))
        {
            _runs++;
            var step = new StepRun(start.WorkflowId().Value, instance.Frame.State, open.Token.Value);
            await Workflow.ExecuteActivityAsync(
                () => RunStepActivities.RunStepAsync(step),
                new ActivityOptions
                {
                    ActivityId = string.Create(CultureInfo.InvariantCulture, $"run:{open.Token.Value}:{_runs}"),
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                }).ConfigureAwait(true);
            _recorded = open.Token.Value;
            _timeoutAt = context.Recipe.States[instance.Frame.State].Timeout is { } timeout ? Now + timeout.After.Span : null;
        }

        return await Wait(instance, context).ConfigureAwait(true);
    }
}
