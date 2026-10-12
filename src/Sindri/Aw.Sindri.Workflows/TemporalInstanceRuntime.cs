using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IInstanceRuntime"/> over the Temporal client (engine spec rev 8 §5.1–§5.4). Unit (b) builds Start
/// (WorkflowIdConflictPolicy.UseExisting, workflow id = the address) and Send's answer, command and cancel paths
/// (<see cref="TemporalCommands"/>: the Update named by the trigger, update id = the nonce, the validator's
/// ApplicationFailure error type mapped back to its code); unit (c) owns Send's 'complete' path,
/// <see cref="TemporalDeskReports"/>; unit (e) builds Get (the 'GetInstance' Query; unit (b) carries a minimal one that
/// gives way to (e)'s); unit (d) builds Signal ('event').
/// </summary>
/// <param name="frontend">Finds the dev server serve keeps.</param>
/// <param name="deskReports">The 'complete' Update (unit (c)).</param>
/// <param name="commands">The answer, command and cancel Updates (unit (b)).</param>
internal sealed class TemporalInstanceRuntime(TemporalFrontend frontend, TemporalDeskReports deskReports, TemporalCommands commands) : IInstanceRuntime
{
    /// <summary>How long a Query waits for a worker to answer.</summary>
    internal static readonly TimeSpan QueryPatience = TimeSpan.FromSeconds(10);

    /// <summary>How often, 50 ms apart, a start asks whether the new instance has left 'starting' (its recipe loaded, its first state entered): 10 s.</summary>
    private const int StartPolls = 200;

    /// <summary>
    /// Starts 'RecipeWorkflow' with the address as its workflow id (spec §5.1): an execution already running under that id
    /// is returned as it is (UseExisting), and an ended one is never started again under the same id (RejectDuplicate;
    /// the slice asks for '--attempt next'). Returns the instance once it has entered its first state.
    /// </summary>
    public async ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        var address = start.WorkflowId();
        try
        {
            _ = await client.Value.StartWorkflowAsync(
                (RecipeWorkflow wf) => wf.RunAsync(start),
                new WorkflowOptions(address.Value, SindriTemporal.TaskQueue)
                {
                    IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
                    IdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate,
                    Rpc = new RpcOptions { CancellationToken = cancellationToken },
                }).ConfigureAwait(false);
        }
        catch (WorkflowAlreadyStartedException) // boundary: the attempt ended; RejectDuplicate keeps its id, and Get shows it as it ended
        {
            return await Get(address, cancellationToken).ConfigureAwait(false);
        }

        for (var poll = 1; ; poll++)
        {
            var view = await Get(address, cancellationToken).ConfigureAwait(false);
            if (view.IsError || view.Value.State != "starting" || poll >= StartPolls)
            {
                return view;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The 'GetInstance' Query on the workflow whose id is <paramref name="address"/>. An ended instance answers too (the
    /// worker replays it). No execution with that id is SND-WF-001. Unit (b)'s minimal Query; unit (e)'s replaces it.
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
            return SindriErrors.ApiTimeout.With($"no worker answered the status query for {address.Value}: {e.Message} (fix: start sindri serve, which runs the worker)");
        }
        catch (WorkflowQueryFailedException e) // boundary: the instance hasn't taken its input yet
        {
            return WorkflowErrors.InstanceNotFound.With($"{address.Value} can't be queried yet: {e.Message} (fix: try again in a moment)");
        }
    }

    public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) => command switch
    {
        StepCompleted report => deskReports.Complete(address, nonce, report, cancellationToken),   // unit (c)
        AnswerGiven or CommandFired or CancelRequested => commands.Send(address, nonce, command, cancellationToken),   // unit (b)
        _ => throw new NotSupportedException($"engine P5: the '{command.Name}' Update"),
    };

    public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P4 unit (d): the bridge's 'event' signal");
}
