using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IProcessTable: the one fake, kept honest by ProcessTableContract.</summary>
public sealed class InMemoryProcessTable : IProcessTable
{
    private readonly List<ProcessInfo> _processes = [];
    private int _next = 5000;

    /// <summary>Starts a process that stays until terminated.</summary>
    /// <returns>Its pid.</returns>
    public int Start()
    {
        var pid = ++_next;
        _processes.Add(new ProcessInfo(pid, Ppid: 1, "sleep 120", RssKb: 512, AgeSeconds: 1));
        return pid;
    }

    /// <summary>Adds a process as given.</summary>
    /// <param name="process">The process.</param>
    public void Add(ProcessInfo process) => _processes.Add(process);

    /// <inheritdoc />
    public ValueTask<Result<HostSnapshot>> Snapshot(CancellationToken cancellationToken) =>
        Done.Of(new HostSnapshot([.. _processes], MemAvailableKb: 8_000_000, SwapUsedKb: 0, DiskFreeBytes: 50_000_000_000));

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Terminate(int pid, CancellationToken cancellationToken) =>
        _processes.RemoveAll(p => p.Pid == pid) > 0
            ? Done.Unit()
            : Done.Failed<Unit>(MetaErrors.HostUnreadable.With("no such process"));
}
