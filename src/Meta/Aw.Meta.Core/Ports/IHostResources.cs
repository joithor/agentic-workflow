using Aw.Kernel;
using Aw.Meta.Core.Health;

namespace Aw.Meta.Core.Ports;

/// <summary>The host's memory and disk figures and the heavy-job locks.</summary>
public interface IHostResources
{
    /// <summary>Reads /proc/meminfo and the state disk's free space.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The figures, or AW-META-010.</returns>
    public ValueTask<Result<HostResources>> Read(CancellationToken cancellationToken);

    /// <summary>Lists the taken slots of the heavy-job lock (the lock path and its .k siblings).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The taken slots, or AW-META-010.</returns>
    public ValueTask<Result<IReadOnlyList<HeavyLock>>> HeavyLocks(CancellationToken cancellationToken);
}
