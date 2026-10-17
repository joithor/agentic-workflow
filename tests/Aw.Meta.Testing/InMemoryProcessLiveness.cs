using Aw.Kernel;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IProcessLiveness: the one fake, kept honest by ProcessLivenessContract.</summary>
public sealed class InMemoryProcessLiveness : IProcessLiveness
{
    private readonly Dictionary<int, DateTimeOffset> _running = [];
    private Error? _failure;

    /// <summary>Makes a pid a running process that started at the given time.</summary>
    /// <param name="pid">The process id.</param>
    /// <param name="startedAt">When it started.</param>
    public void Run(int pid, DateTimeOffset startedAt) => _running[pid] = startedAt;

    /// <summary>Ends a process.</summary>
    /// <param name="pid">The process id.</param>
    public void Stop(int pid) => _ = _running.Remove(pid);

    /// <summary>Makes every call fail, as a host that can't be asked does.</summary>
    /// <param name="error">The error.</param>
    public void FailWith(Error error) => _failure = error;

    /// <inheritdoc />
    public ValueTask<Result<bool>> IsRunning(int pid, DateTimeOffset recordedAt, CancellationToken cancellationToken) =>
        _failure is { } error
            ? Done.Failed<bool>(error)
            : Done.Of(pid > 0 && _running.TryGetValue(pid, out var startedAt) && !ProcessStart.IsReused(startedAt, recordedAt));
}
