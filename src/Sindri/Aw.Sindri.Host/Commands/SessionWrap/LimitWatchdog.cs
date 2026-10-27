using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Host.Commands.SessionWrap;

#pragma warning disable MA0182 // until the wrap uses it (next commit)
/// <summary>
/// The only clock of a sandboxed session (sandbox spec §3.3; SB-3a): one tick every 5 s reads the scope's accounting, asks the control plane whether the run is
/// suspended, counts unsuspended time and, when the wall is used up, ends the scope with TERM, then KILL 10 s later. It also tells a memory kill from any other end.
/// </summary>
/// <param name="scope">systemd's view of the scope.</param>
/// <param name="stats">The scope's accounting files.</param>
/// <param name="calls">The control plane calls (the suspension read).</param>
/// <param name="clock">The time the ticks run on.</param>
/// <param name="limits">The limits file.</param>
internal sealed class LimitWatchdog(IScopeControl scope, ICgroupStats stats, IWrapCalls calls, TimeProvider clock, WrapLimits limits)
{
    /// <summary>Watches until the agent exits.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="proof">The launch secret, or null when wrap has none (then nothing is ever suspended).</param>
    /// <param name="childExited">Completes when the agent exits.</param>
    /// <param name="cancellationToken">Cancels the watch.</param>
    /// <returns>The limit that ended the session, or null when none did.</returns>
    internal Task<LimitEnd?> Run(LaunchId launch, LaunchSecret? proof, Task childExited, CancellationToken cancellationToken) =>
        throw new NotSupportedException(string.Concat(scope, stats, calls, clock, limits, launch, proof, childExited, cancellationToken));
}
#pragma warning restore MA0182
