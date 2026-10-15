using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>Sends SIGTERM to orphans, one pid at a time, and logs each kill as an incident. Only processes the orphan rule already matched are given to it.</summary>
/// <param name="processes">Terminates a process by pid.</param>
/// <param name="log">The running log.</param>
/// <param name="clock">Dates the incident.</param>
internal sealed class OrphanKiller(IProcessTable processes, IRunningLog log, TimeProvider clock)
{
    /// <summary>Terminates the orphans. One that has already gone is skipped.</summary>
    /// <returns>The pids that were terminated.</returns>
    internal async ValueTask<Result<IReadOnlyList<int>>> Kill(IReadOnlyList<ProcessInfo> orphans, CancellationToken cancellationToken)
    {
        var killed = new List<int>();
        foreach (var orphan in orphans)
        {
            if ((await processes.Terminate(orphan.Pid, cancellationToken).ConfigureAwait(false)).IsError)
            {
                continue;
            }

            killed.Add(orphan.Pid);
            var text = string.Create(CultureInfo.InvariantCulture, $"health --kill-orphans sent SIGTERM to orphaned temporal dev server pid {orphan.Pid} (up {orphan.AgeSeconds / 60} min)");
            var logged = await log.Append(LogLine.Event(LogKind.Incident, clock.GetUtcNow(), text), cancellationToken).ConfigureAwait(false);
            if (logged.IsError)
            {
                return logged.Error;
            }
        }

        return killed;
    }
}
