using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IRecipeWake"/> over the Temporal client (engine spec §4.1, W11; engine P6 integration): the running
/// instances of a hash are the open executions whose 'RecipeHash' search attribute is it, and a wake is the 'wake'
/// signal each takes, acting as 'service:engine' for the approver. With no Temporal serving this home nothing runs, so
/// there is nobody to wake and the registry change still stands.
/// </summary>
/// <param name="frontend">Finds the dev server.</param>
internal sealed class TemporalRecipeWake(TemporalFrontend frontend) : IRecipeWake
{
    private const string Actor = "service:engine";

    public async ValueTask<Result<IReadOnlyList<WorkflowInstanceId>>> Running(RecipeHash hash, CancellationToken cancellationToken)
    {
        var open = await Open(hash, cancellationToken).ConfigureAwait(false);
        WorkflowInstanceId[] running = [.. open.Select(static a => WorkflowInstanceId.Of(a))];
        return running;
    }

    public async ValueTask<Result<Unit>> Wake(RecipeWakeNotice notice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notice);
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return Unit.Value;   // nothing serves this home: no instance runs, so none needs the wake
        }

        var wake = new RecipeWake(Actor, notice.Kind, notice.Sequence);
        foreach (var address in await Open(notice.Hash, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await client.Value.GetWorkflowHandle<RecipeWorkflow>(address.Value).SignalAsync(
                    wf => wf.WakeAsync(wake), new WorkflowSignalOptions { Rpc = new RpcOptions { CancellationToken = cancellationToken } }).ConfigureAwait(false);
            }
            catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)   // boundary: it ended between the list and the signal
            {
                continue;
            }
        }

        return Unit.Value;
    }

    /// <summary>The open instances running <paramref name="hash"/>; none when nothing serves this home.</summary>
    private async ValueTask<IReadOnlyList<InstanceAddress>> Open(RecipeHash hash, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return [];
        }

        var addresses = new List<InstanceAddress>();
        try
        {
            await foreach (var execution in client.Value.ListWorkflowsAsync($"WorkflowType = 'RecipeWorkflow' AND ExecutionStatus = 'Running' AND RecipeHash = '{hash.Value}'")
                .WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                addresses.Add(InstanceAddress.From(execution.Id));
            }
        }
        catch (RpcException e) when (e.Code is RpcException.StatusCode.NotFound or RpcException.StatusCode.InvalidArgument)   // boundary: not serve's Temporal, or its visibility has no RecipeHash yet
        {
            return [];
        }

        return addresses;
    }
}
