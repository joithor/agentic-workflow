using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.PingProcess;

/// <summary>
/// The pid and the held epoch; without a lease (stopping, or lost) this process isn't the control plane, so it refuses
/// with SND-API-001 rather than answer an epoch no record can match.
/// </summary>
internal sealed class PingProcessService(HeldLease held, IProcessProbe probe) : IService<PingProcessRequest, Ping>
{
    public ValueTask<Result<Ping>> Handle(PingProcessRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<Ping>>(held.Current is { } grant
            ? new Ping(probe.Self().Pid, grant.Epoch)
            : SindriErrors.NotRunning.With("this process holds no lease: it is stopping, or lost it (fix: sindri)"));
}
