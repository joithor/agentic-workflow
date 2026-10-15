using System.Linq.Expressions;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The answer, command and cancel paths of <see cref="TemporalInstanceRuntime.Send"/> (engine spec rev 8 §5.2; engine
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
        _ => throw new NotSupportedException($"engine P5: the '{command.Name}' Update"),
    };

    private async ValueTask<Result<CommandResult>> Execute(InstanceAddress address, CommandNonce nonce, Expression<Func<RecipeWorkflow, Task<CommandResult>>> update, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        try
        {
            return await client.Value.GetWorkflowHandle<RecipeWorkflow>(address.Value).ExecuteUpdateAsync(
                update,
                new WorkflowUpdateOptions(nonce.Value) { Rpc = TemporalUpdates.Rpc(cancellationToken) }).ConfigureAwait(false);
        }
        catch (WorkflowUpdateFailedException e) when (e.InnerException is ApplicationFailureException { ErrorType: { } code } refusal) // boundary: the validator's refusal is the caller's answer
        {
            return (WorkflowErrors.All.FirstOrDefault(d => d.Code == code) ?? WorkflowErrors.TriggerNotAllowed).With(refusal.Message);
        }
        catch (RpcException e) when (TemporalUpdates.NoNamespace(e)) // boundary: not serve's Temporal; says nothing about the instance
        {
            return TemporalUpdates.NotServes(client.Value, e);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested && e is WorkflowUpdateRpcTimeoutOrCanceledException or RpcException { Code: RpcException.StatusCode.DeadlineExceeded }) // boundary: no worker took it
        {
            return TemporalUpdates.NoWorker(address, nonce);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound) // boundary: no running execution with that id
        {
            return WorkflowErrors.InstanceNotFound.With($"no running instance {address.Value} (fix: sindri workflow list)");
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.Unavailable) // boundary: the dev server died under a live serve
        {
            return TemporalUpdates.Down(client.Value, e, nonce);
        }
    }
}
