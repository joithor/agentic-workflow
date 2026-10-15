using Aw.Kernel;
using Aw.Meta.Core.Health;

namespace Aw.Meta.Core.Ports;

/// <summary>The host's processes and memory.</summary>
public interface IProcessTable
{
    /// <summary>Reads every process and the host's memory and disk figures. Zombies are left out.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The snapshot, or AW-META-010.</returns>
    public ValueTask<Result<HostSnapshot>> Snapshot(CancellationToken cancellationToken);

    /// <summary>Sends SIGTERM to one process by pid.</summary>
    /// <param name="pid">The process id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or AW-META-010 when the process can't be signalled.</returns>
    public ValueTask<Result<Unit>> Terminate(int pid, CancellationToken cancellationToken);
}
