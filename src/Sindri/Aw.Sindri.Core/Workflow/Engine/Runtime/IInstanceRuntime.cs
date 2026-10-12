using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The instances on Temporal, as the slices see them (port; engine spec rev 8 §5.1–§5.4). The adapter is the Temporal
/// client in Aw.Sindri.Workflows; every caller check (the API chain, the nonce, the approval check) runs before it.
/// </summary>
public interface IInstanceRuntime
{
    /// <summary>Starts a 'RecipeWorkflow' with id <see cref="InstanceStart.WorkflowId"/>, or finds the running one (UseExisting).</summary>
    /// <param name="start">The workflow input.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The instance as 'GetInstance' shows it, or SND-API-001 (Temporal unreachable).</returns>
    public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken);

    /// <summary>The 'GetInstance' Query.</summary>
    /// <param name="address">The instance.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The instance, or SND-WF-001.</returns>
    public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken);

    /// <summary>
    /// Sends <paramref name="command"/> as the Update its kind names ('complete', 'answer', 'command', 'cancel',
    /// 'takeover', 'handback'), with <paramref name="nonce"/> as the update id: a repeated nonce returns the first outcome.
    /// </summary>
    /// <param name="address">The instance.</param>
    /// <param name="nonce">The nonce, the Update's id.</param>
    /// <param name="command">The trigger: StepCompleted, AnswerGiven, CommandFired, CancelRequested, TakeoverRequested or HandbackRequested.</param>
    /// <param name="cancellationToken">Cancels the wait for the outcome.</param>
    /// <returns>The outcome, or the validator's refusal (SND-WF-003, -005, -008) with nothing recorded, or SND-WF-001.</returns>
    public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken);

    /// <summary>Signals a registered event to the instance (the bridge, §5.4); Temporal buffers it until the instance reaches its wait.</summary>
    /// <param name="address">The instance the event routes to.</param>
    /// <param name="delivered">The event as the wait's trigger.</param>
    /// <param name="cancellationToken">Cancels the signal.</param>
    /// <returns>Done, or SND-WF-001.</returns>
    public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken);
}
