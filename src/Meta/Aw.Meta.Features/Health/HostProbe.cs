using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>Reads the host: processes, memory and disk, heavy-job locks.</summary>
/// <param name="processes">The process table.</param>
/// <param name="resources">Memory, disk and locks.</param>
internal sealed class HostProbe(IProcessTable processes, IHostResources resources)
{
    internal async ValueTask<Result<HostReading>> Read(CancellationToken cancellationToken)
    {
        var snapshot = await processes.Snapshot(cancellationToken).ConfigureAwait(false);
        var host = await resources.Read(cancellationToken).ConfigureAwait(false);
        var locks = await resources.HeavyLocks(cancellationToken).ConfigureAwait(false);
        if (snapshot.IsError || host.IsError || locks.IsError)
        {
            return snapshot.IsError ? snapshot.Error : host.IsError ? host.Error : locks.Error;
        }

        return new HostReading(snapshot.Value, host.Value, locks.Value);
    }
}
