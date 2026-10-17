using Aw.Kernel;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>The host ports on an OS aw-meta can't read the process table and memory of: every call is AW-META-010 naming the OS, never a made-up figure.</summary>
/// <param name="os">The OS's name.</param>
internal sealed class UnsupportedHost(string os) : IProcessTable, IHostResources, IProcessLiveness
{
    public ValueTask<Result<HostSnapshot>> Snapshot(CancellationToken cancellationToken) => throw new NotSupportedException("red: " + os);

    public ValueTask<Result<Unit>> Terminate(int pid, CancellationToken cancellationToken) => throw new NotSupportedException("red: " + os);

    public ValueTask<Result<HostResources>> Read(CancellationToken cancellationToken) => throw new NotSupportedException("red: " + os);

    public ValueTask<Result<IReadOnlyList<HeavyLock>>> HeavyLocks(CancellationToken cancellationToken) => throw new NotSupportedException("red: " + os);

    public ValueTask<Result<bool>> IsRunning(int pid, DateTimeOffset recordedAt, CancellationToken cancellationToken) => throw new NotSupportedException("red: " + os);
}
