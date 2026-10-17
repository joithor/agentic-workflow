using Aw.Kernel;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The <see cref="ILaunchProbes"/> over the ledger. The key carries the sequence of the probe it replaces, so a state that comes back (A, B, A) is a new
/// fact, and two probes that race over the same predecessor record one.
/// </summary>
/// <param name="launcher">Probes the viewer, the host and the catalog.</param>
/// <param name="approvals">The approved profile, which names the repos.</param>
/// <param name="locator">Says whether a repo's path is a checkout.</param>
/// <param name="ledger">The ledger.</param>
/// <param name="events">Builds the entry.</param>
internal sealed class LaunchProbeRecorder(ILauncher launcher, IApprovedProfile approvals, IRepoLocator locator, ILedgerStore ledger, EventFactory events) : ILaunchProbes
{
    /// <inheritdoc/>
    public ValueTask<Result<RecordedProbe>> Record(LeaseGrant grant, CancellationToken cancellationToken)
    {
        _ = (launcher, approvals, locator, ledger, events);
        throw new NotSupportedException("LaunchProbeRecorder: make the probe tests pass");
    }
}
