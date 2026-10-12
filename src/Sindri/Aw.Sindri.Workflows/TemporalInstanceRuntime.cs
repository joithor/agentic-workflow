using Aw.Kernel;
using Aw.Sindri.Core;
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
/// <param name="frontend">Finds the dev server serve keeps (Get, unit (e); the 'event' signal, unit (d)).</param>
internal sealed class TemporalInstanceRuntime(TemporalFrontend frontend, TemporalDeskReports deskReports) : IInstanceRuntime
{
    /// <summary>How long a Query waits for a worker to answer; past it, status says no worker answered.</summary>
    internal static readonly TimeSpan QueryPatience = TimeSpan.FromSeconds(10);

    public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P4 unit (b): the start, through TemporalFrontend");

    /// <summary>
    /// The 'GetInstance' Query on the workflow whose id is <paramref name="address"/> (unit (e)). An ended instance answers
    /// too (the worker replays it). No execution with that id is SND-WF-001; a Query no worker answers in
    /// <see cref="QueryPatience"/> is SND-API-002.
    /// </summary>
    public async ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        try
        {
            return await client.Value.GetWorkflowHandle<RecipeWorkflow>(address.Value)
                .QueryAsync(static wf => wf.GetInstance(), new WorkflowQueryOptions { Rpc = new RpcOptions { Timeout = QueryPatience, CancellationToken = cancellationToken } })
                .ConfigureAwait(false);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound) // boundary: no such execution is the caller's input
        {
            return WorkflowErrors.InstanceNotFound.With($"{address.Value} isn't an instance (fix: sindri workflow list, or sindri workflow start)");
        }
        catch (RpcException e) when (e.Code is RpcException.StatusCode.DeadlineExceeded or RpcException.StatusCode.Unavailable) // boundary: no worker answered
        {
            return SindriErrors.ApiTimeout.With($"no worker answered the status query for {address.Value} within {StatusText.Duration(QueryPatience)}: {e.Message} (fix: start sindri serve, which runs the worker)");
        }
        catch (WorkflowQueryFailedException e) // boundary: the instance hasn't taken its input yet
        {
            return WorkflowErrors.InstanceNotFound.With($"{address.Value} can't be queried yet: {e.Message} (fix: try again in a moment)");
        }
    }

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
