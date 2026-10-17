using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.Launching;

/// <summary>
/// Appends what the launcher service decides (UI spec E-1). 'launch.started' and 'launch.failed' share one fact key, 'launch.outcome:&lt;launch&gt;',
/// so the first outcome written wins and the second comes back 'AlreadyRecorded'.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="events">Builds the entries.</param>
internal sealed class LaunchOutcomes(ILedgerStore ledger, EventFactory events)
{
    /// <summary>Records the session as started.</summary>
    /// <param name="grant">The lease.</param>
    /// <param name="started">The payload.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The append; 'AlreadyRecorded' when another outcome got there first.</returns>
    public ValueTask<Result<LedgerAppend>> Started(LeaseGrant grant, LaunchStarted started, CancellationToken cancellationToken) =>
        ledger.Append(events.Entry(EventTypes.LaunchStarted, started, OutcomeKey(started.Launch), started.Item.Value), grant, cancellationToken);

    /// <summary>Records the launch as failed.</summary>
    /// <param name="grant">The lease.</param>
    /// <param name="failed">The payload.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The append; 'AlreadyRecorded' when another outcome got there first.</returns>
    public ValueTask<Result<LedgerAppend>> Failed(LeaseGrant grant, LaunchFailed failed, CancellationToken cancellationToken) =>
        ledger.Append(events.Entry(EventTypes.LaunchFailed, failed, OutcomeKey(failed.Launch), failed.Item.Value), grant, cancellationToken);

    /// <summary>Records that a client attached to an 'unconfirmed' launch's session.</summary>
    /// <param name="grant">The lease.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="item">Its item.</param>
    /// <param name="at">When the client was seen.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The append.</returns>
    public ValueTask<Result<LedgerAppend>> ViewerAttached(LeaseGrant grant, LaunchId launch, ItemId item, DateTimeOffset at, CancellationToken cancellationToken) =>
        ledger.Append(events.Entry(EventTypes.LaunchViewerAttached, new LaunchViewerAttached(launch, at), $"{EventTypes.LaunchViewerAttached}:{launch.Value}", item.Value), grant, cancellationToken);

    private static string OutcomeKey(LaunchId launch) => "launch.outcome:" + launch.Value;
}
