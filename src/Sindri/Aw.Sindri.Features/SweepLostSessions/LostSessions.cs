using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.SweepLostSessions;

/// <summary>
/// Ends the sessions with no heartbeat for <see cref="LaunchRules.LostAfter"/> as 'session.ended' with reason lost (API spec §8.1), and the ones whose turn ended with no report for <see cref="LaunchRules.IdleAfter"/> as idle. A host that runs its
/// sessions without 'session wrap' (T3) is asked first: each live session it reports counts as a heartbeat, and one it reports gone ends as an exit.
/// </summary>
/// <param name="liveness">The heartbeats.</param>
/// <param name="session">The session events.</param>
/// <param name="clock">The time.</param>
/// <param name="files">The per-launch files.</param>
/// <param name="views">The 'launches' view: which launches have a live session.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="hosted">The sessions a host keeps without a wrap.</param>
/// <param name="instances">Reads the instance a Step launch belongs to, to see whether a person took it over.</param>
internal sealed class LostSessions(LaunchLiveness liveness, LaunchSessionEvents session, TimeProvider clock, ILaunchFiles files, ILaunchViews views, IProjectorStatus status, IHostedSessions hosted, IInstanceReader instances) : ILostSessions
{
    public async ValueTask<int> Sweep(CancellationToken cancellationToken)
    {
        await SweepHosted(cancellationToken).ConfigureAwait(false);
        var ended = 0;
        foreach (var launch in liveness.SilentAfterSweep(clock.GetUtcNow(), LaunchRules.LostAfter, LaunchRules.SweepGap))
        {
            if ((await session.End(launch, SessionEndReason.Lost, exitCode: null, cancellationToken).ConfigureAwait(false)).IsSuccess)
            {
                hosted.Forget(launch);
                ended++;
            }
        }

        return ended + await SweepIdle(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends as idle the sessions whose Stop hook says the turn ended <see cref="LaunchRules.IdleAfter"/> ago: the agent has nothing more to do and never reported, and its wrap still
    /// beats, so the silence sweep never sees it. A prompt deletes the hook's file, so a person typing to the session starts the clock over.
    /// </summary>
    private async ValueTask<int> SweepIdle(CancellationToken cancellationToken)
    {
        var ended = 0;
        var now = clock.GetUtcNow();
        foreach (var launch in liveness.Active())
        {
            if (files.TurnEndedAt(launch) is { } turn && now - turn >= LaunchRules.IdleAfter
                && (await session.End(launch, SessionEndReason.Idle, exitCode: null, cancellationToken).ConfigureAwait(false)).IsSuccess)
            {
                hosted.Forget(launch);
                ended++;
            }
        }

        return ended;
    }

    /// <summary>Heartbeats for the live hosted sessions Sindri tracks; an exit for the ones whose host says they are gone (not counted as lost).</summary>
    private async ValueTask SweepHosted(CancellationToken cancellationToken)
    {
        var tracked = liveness.Active().ToHashSet();
        foreach (var (launch, alive) in await hosted.Check(cancellationToken).ConfigureAwait(false))
        {
            if (alive)
            {
                if (tracked.Contains(launch))
                {
                    liveness.Beat(launch, clock.GetUtcNow());   // only a launch Sindri still counts as live: a stale map entry never revives an ended one
                }

                continue;
            }

            if (!tracked.Contains(launch) || (await session.End(launch, SessionEndReason.Exit, exitCode: null, cancellationToken).ConfigureAwait(false)).IsSuccess)
            {
                hosted.Forget(launch);   // an end that the ledger refused is tried again on the next sweep
            }
        }
    }

    public async ValueTask<int?> SweepFiles(TimeSpan olderThan, CancellationToken cancellationToken)
    {
        var freshness = status.Current.AsOf;
        if (freshness.Rebuilding || freshness.Stalled || freshness.Cursor < freshness.LedgerHead)
        {
            return null;
        }

        var live = new HashSet<LaunchId>();
        foreach (var launch in files.Launches())
        {
            var row = await views.Get(launch, cancellationToken).ConfigureAwait(false);
            if (row.IsSuccess && row.Value is { Live: true })
            {
                _ = live.Add(launch);
            }
        }

        return files.SweepStale(clock.GetUtcNow(), olderThan, live.Contains);
    }
}
