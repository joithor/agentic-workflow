using System.Linq.Expressions;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Client;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The answer, command, cancel, takeover, handback and open-request paths of <see cref="TemporalInstanceRuntime.Send"/> (engine spec rev 8 §5.2; engine
/// P4 unit (b)): each trigger as the Update its kind names on the instance's workflow, with the nonce as the update id,
/// so Temporal returns the first outcome for a repeated nonce. The validator's refusal comes back as the
/// ApplicationFailure its error type names (SND-WF-003 …), mapped back to its catalogue code.
/// </summary>
/// <param name="frontend">Finds the dev server serve keeps.</param>
internal sealed class TemporalCommands(TemporalFrontend frontend)
{
    /// <summary>Sends <paramref name="command"/> as its Update keyed by <paramref name="nonce"/>.</summary>
    /// <param name="address">The instance (its workflow id).</param>
    /// <param name="nonce">The update id.</param>
    /// <param name="command">An answer, a recipe command or a cancel.</param>
    /// <param name="cancellationToken">Cancels the wait for the outcome.</param>
    /// <returns>
    /// The outcome, the validator's refusal, SND-WF-001 for no such running instance, SND-API-001 for a Temporal without
    /// the namespace, or SND-API-002 when no worker took the Update within <see cref="TemporalUpdates.Patience"/>.
    /// </returns>
    public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) => command switch
    {
        AnswerGiven answer => Execute(address, nonce, wf => wf.AnswerAsync(answer), cancellationToken),
        CommandFired fired => Execute(address, nonce, wf => wf.CommandAsync(fired), cancellationToken),
        CancelRequested cancel => Execute(address, nonce, wf => wf.CancelAsync(cancel), cancellationToken),
        TakeoverRequested takeover => Execute(address, nonce, wf => wf.TakeoverAsync(takeover), cancellationToken),
        HandbackRequested handback => Execute(address, nonce, wf => wf.HandbackAsync(handback), cancellationToken),
        _ => throw new NotSupportedException($"the '{command.Name}' trigger is no command Update"),
    };

    /// <summary>
    /// The 'open-request' Update (UI spec E-6, spec §5.2) with update id 'wf:{instance}:open:{launch}': no nonce, and
    /// Temporal returns the first outcome for a repeat.
    /// </summary>
    /// <param name="address">The instance.</param>
    /// <param name="request">The open-request.</param>
    /// <param name="cancellationToken">Cancels the wait for the outcome.</param>
    /// <returns>The outcome, or the validator's refusal (SND-WF-003), or SND-WF-001.</returns>
    public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress address, OpenRequested request, CancellationToken cancellationToken) =>
        Execute(address, InstanceIds.OpenRequestUpdate(address, request), wf => wf.OpenRequestAsync(request), cancellationToken);

    private ValueTask<Result<CommandResult>> Execute(InstanceAddress address, CommandNonce nonce, Expression<Func<RecipeWorkflow, Task<CommandResult>>> update, CancellationToken cancellationToken) =>
        Execute(address, nonce.Value, update, cancellationToken);

    private async ValueTask<Result<CommandResult>> Execute(InstanceAddress address, string updateId, Expression<Func<RecipeWorkflow, Task<CommandResult>>> update, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        var handle = client.Value.GetWorkflowHandle<RecipeWorkflow>(address.Value);
        return await TemporalUpdates.Execute(
            TemporalUpdates.UpdateFrontend.Of(client.Value),
            address,
            updateId,
            rpc => handle.ExecuteUpdateAsync(update, new WorkflowUpdateOptions(updateId) { Rpc = rpc }),
            cancellationToken).ConfigureAwait(false);
    }
}
