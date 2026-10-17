using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.Launching;

/// <summary>
/// One tick of the launcher service (UI spec E-1; caller 'service:launcher'): the start-up sweep once, then each tick reads the ledger from the cursor,
/// fails the requests past their deadline that nobody claimed (SND-LAUNCH-007, under the shared 'launch.outcome' key), polls the viewers still
/// 'unconfirmed' (appending 'launch.viewer-attached' once), and hands out the requests that are new. It launches nothing itself: the host runs
/// <see cref="ProcessLaunch"/> for each, so a slow takeover never holds up the expiry.
/// </summary>
/// <param name="recovery">The start-up sweep.</param>
/// <param name="intake">Reads the ledger.</param>
/// <param name="outcomes">Appends the failures and the attach.</param>
/// <param name="host">Counts a session's attached clients.</param>
/// <param name="probes">Probes the launch targets: at start, every 10 minutes and after a failed launch.</param>
/// <param name="work">What the launcher remembers.</param>
/// <param name="timings">The limits.</param>
internal sealed class RunLaunch(LaunchRecovery recovery, LaunchIntake intake, LaunchOutcomes outcomes, ISessionHost host, ILaunchProbes probes, LaunchWork work, LaunchTimings timings)
    : IService<RunLaunchRequest, RunLaunchResponse>
{
    public async ValueTask<Result<RunLaunchResponse>> Handle(RunLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!work.Recovered)
        {
            var swept = await recovery.Run(request.Grant, cancellationToken).ConfigureAwait(false);
            if (swept.IsError)
            {
                return swept.Error;
            }
        }

        var read = await intake.Read(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var expired = await Expire(request.Grant, cancellationToken).ConfigureAwait(false);
        if (expired.IsError)
        {
            return expired.Error;
        }

        var polled = await Poll(request.Grant, cancellationToken).ConfigureAwait(false);
        _ = probes;   // red: no probe is recorded yet
        return polled.IsError ? polled.Error : new RunLaunchResponse(work.TakeDue());
    }

    private async ValueTask<Result<Unit>> Expire(LeaseGrant grant, CancellationToken cancellationToken)
    {
        foreach (var pending in work.Expired(timings))
        {
            var failed = new LaunchFailed(pending.Request.Launch, pending.Request.Item, SindriErrors.LaunchInterrupted.Code,
                "The launch did not start within 30 s (fix: open the item again)", Stage: null);
            var appended = await outcomes.Failed(grant, failed, cancellationToken).ConfigureAwait(false);
            if (appended.IsError)
            {
                return appended.Error;
            }

            work.Settle(pending.Request.Launch);   // a claim made since the read above still launches: it owns the outcome key and wins or loses it there
        }

        return Unit.Value;
    }

    private async ValueTask<Result<Unit>> Poll(LeaseGrant grant, CancellationToken cancellationToken)
    {
        var now = timings.Clock.GetUtcNow();
        foreach (var watched in work.Watched())
        {
            if (now >= watched.Until)
            {
                work.Unwatch(watched.Launch);
            }
            else if (await host.AttachedClients(watched.Host, cancellationToken).ConfigureAwait(false) > 0)
            {
                var appended = await outcomes.ViewerAttached(grant, watched, now, cancellationToken).ConfigureAwait(false);
                if (appended.IsError)
                {
                    return appended.Error;
                }

                work.Unwatch(watched.Launch);
            }
        }

        return Unit.Value;
    }
}
