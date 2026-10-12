using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IInstanceRuntime"/> over the Temporal client (engine spec rev 8 §5.1–§5.4). Engine P4 contract: stubs.
/// Unit (b) builds Start (WorkflowIdConflictPolicy.UseExisting, workflow id = the address) and Send (the Update named
/// by the trigger, update id = the nonce, the validator's ApplicationFailure error type mapped back to its code);
/// unit (e) builds Get (the 'GetInstance' Query, SND-WF-001 for no execution); unit (d) builds Signal ('event').
/// Unit (c) owns Send's 'complete' path, <see cref="TemporalDeskReports"/>; the other Updates are unit (b)'s.
/// </summary>
/// <param name="deskReports">The 'complete' Update (unit (c)).</param>
/// <param name="frontend">Finds the dev server serve keeps (unit (d): the 'event' signal).</param>
internal sealed class TemporalInstanceRuntime(TemporalDeskReports deskReports, TemporalFrontend frontend) : IInstanceRuntime
{
    public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P4 unit (b): the start, through TemporalFrontend");

    public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P4 unit (e): the GetInstance Query");

    public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) => command switch
    {
        StepCompleted report => deskReports.Complete(address, nonce, report, cancellationToken),   // unit (c)
        _ => throw new NotSupportedException("engine P4 unit (b): command Updates keyed by the nonce"),
    };

    /// <summary>
    /// The bridge's 'event' signal (spec §5.4; unit (d)): Temporal buffers it until the instance reaches the wait. The
    /// event's ledger sequence is its idempotency key: the workflow drops a sequence it has already taken.
    /// </summary>
    public async ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        try
        {
            await client.Value.GetWorkflowHandle<RecipeWorkflow>(address.Value).SignalAsync(
                wf => wf.EventAsync(delivered),
                new WorkflowSignalOptions { Rpc = new RpcOptions { CancellationToken = cancellationToken } }).ConfigureAwait(false);
            return Unit.Value;
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)   // boundary: no such instance, or it has ended
        {
            return WorkflowErrors.InstanceNotFound.With($"no running instance {address.Value} (fix: sindri workflow list)");
        }
    }
}
