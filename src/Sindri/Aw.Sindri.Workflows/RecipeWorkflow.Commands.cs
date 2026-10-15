using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The command Updates of <see cref="RecipeWorkflow"/> (engine spec rev 8 §5.2, §5.8; engine P4 unit (b)): 'answer' in
/// its variants, 'command &lt;verb&gt;' and 'cancel'. Each validator runs the same pure <see cref="TransitionDecision"/> the
/// loop runs, on the instance as it stands, and rejects what it rejects (SND-WF-003 for a trigger not allowed here, an
/// answer that doesn't fit the park with the fix naming the one that does), so a rejected Update records nothing and
/// consumes nothing (W12). An accepted Update hands its trigger to the loop through <see cref="Submit"/> and returns the
/// instance after it and what happened. The Update id is the nonce, so a repeat returns the first outcome.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    /// <summary>'workflow answer' in all its variants (spec §5.2, §5.8): ends the suspension it fits and runs its resume action.</summary>
    /// <param name="answer">The answer.</param>
    /// <returns>The instance after it, and the resume action it ran ('resumed (transition): -> produce').</returns>
    [WorkflowUpdate("answer")]
    public async Task<CommandResult> AnswerAsync(AnswerGiven answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var before = _instance!; // why: the validator refused the Update while the instance was starting
        var decision = await Submit(answer).ConfigureAwait(true);
        var happened = Outcome(before, decision);
        var resume = answer.Variant == AnswerVariant.Reject ? ResumeKind.Refuse : before.Suspension?.Resume.Kind;
        return new CommandResult(happened.Instance, decision is Absorbed ? happened.Happened : $"resumed ({ResumeName(resume)}): {happened.Happened}");
    }

    /// <summary>Rejects an answer that doesn't fit the park reason (SND-WF-003, the fix naming the right answer), recording nothing.</summary>
    /// <param name="answer">The answer.</param>
    [WorkflowUpdateValidator(nameof(AnswerAsync))]
    public void ValidateAnswer(AnswerGiven answer) => Validate(answer);

    /// <summary>'workflow command &lt;verb&gt;' (spec §3.5, §6.3): fires the recipe's 'command.&lt;verb&gt;' trigger.</summary>
    /// <param name="command">The command.</param>
    /// <returns>The instance after it, and what happened.</returns>
    [WorkflowUpdate("command")]
    public async Task<CommandResult> CommandAsync(CommandFired command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var before = _instance!; // why: the validator refused the Update while the instance was starting
        var happened = Outcome(before, await Submit(command).ConfigureAwait(true));
        return new CommandResult(happened.Instance, happened.Happened);
    }

    /// <summary>Rejects a verb with no transition from the current state (SND-WF-003), recording nothing.</summary>
    /// <param name="command">The command.</param>
    [WorkflowUpdateValidator(nameof(CommandAsync))]
    public void ValidateCommand(CommandFired command) => Validate(command);

    /// <summary>'workflow cancel' (spec §3.8): ends the instance as 'cancelled'; exempt from the approval check.</summary>
    /// <param name="cancel">The cancel.</param>
    /// <returns>The instance after it.</returns>
    [WorkflowUpdate("cancel")]
    public async Task<CommandResult> CancelAsync(CancelRequested cancel)
    {
        ArgumentNullException.ThrowIfNull(cancel);
        var before = _instance!; // why: the validator refused the Update while the instance was starting
        var happened = Outcome(before, await Submit(cancel).ConfigureAwait(true));
        return new CommandResult(happened.Instance, happened.Happened);
    }

    /// <summary>Rejects a cancel of an ended instance (SND-WF-003), recording nothing.</summary>
    /// <param name="cancel">The cancel.</param>
    [WorkflowUpdateValidator(nameof(CancelAsync))]
    public void ValidateCancel(CancelRequested cancel) => Validate(cancel);

    /// <summary>
    /// What the validators share: the instance must have started, and the pure decision must not reject the trigger on
    /// the instance as it stands. It reads state only.
    /// </summary>
    private void Validate(Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        if (_instance is not { } instance || _context is not { } context)
        {
            throw Refusal(WorkflowErrors.TriggerNotAllowed.With($"{_start?.WorkflowId().Value} is still starting (fix: run status, then send it again)"));
        }

        if (TransitionDecision.Decide(new DecisionInput(instance, context.Recipe, trigger, GateAnswers.None, context.Decisions)) is Rejected rejected)
        {
            throw Refusal(rejected.Error);
        }
    }

    /// <summary>
    /// The instance after the loop decided the command, and what happened in the words the CLI prints; a decision the loop
    /// rejected (the instance moved between the validator and the loop) fails the Update with its code, recording nothing.
    /// </summary>
    private (InstanceView Instance, string Happened) Outcome(WorkflowInstance before, Decision decision)
    {
        if (decision is Rejected rejected)
        {
            throw Refusal(rejected.Error);
        }

        return (GetInstance(), Happened(before, _instance!, decision)); // why: set by Begin before any Update is accepted
    }

    private static string ResumeName(ResumeKind? kind) => kind switch
    {
        ResumeKind.Transition => "transition",
        ResumeKind.Rerun => "rerun",
        ResumeKind.Reevaluate => "reevaluate",
        ResumeKind.Rewait => "rewait",
        ResumeKind.Rearm => "rearm",
        ResumeKind.Restart => "restart",
        ResumeKind.Reemit => "reemit",
        ResumeKind.Override => "override",
        ResumeKind.Forward => "forward",
        ResumeKind.Return => "return",
        ResumeKind.Refuse => "refuse",
        ResumeKind.None or null or _ => "none",
    };
}
