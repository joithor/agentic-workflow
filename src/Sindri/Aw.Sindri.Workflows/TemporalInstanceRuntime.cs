using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IInstanceRuntime"/> over the Temporal client (engine spec rev 8 §5.1–§5.4). Engine P4 contract: stubs.
/// Unit (b) builds Start (WorkflowIdConflictPolicy.UseExisting, workflow id = the address) and Send (the Update named
/// by the trigger, update id = the nonce, the validator's ApplicationFailure error type mapped back to its code);
/// unit (e) builds Get (the 'GetInstance' Query, SND-WF-001 for no execution); unit (d) builds Signal ('event').
/// </summary>
internal sealed class TemporalInstanceRuntime : IInstanceRuntime
{
    public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P4 unit (b): the start, through TemporalFrontend");

    public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P4 unit (e): the GetInstance Query");

    public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P4 unit (b): command Updates keyed by the nonce");

    public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P4 unit (d): the bridge's 'event' signal");
}
