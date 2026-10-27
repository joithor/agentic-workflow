using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// Whether a launch's run is suspended, read from the ledger (sandbox spec §7, SB16): never a Temporal query per watchdog beat and never views.db, whose
/// projector may lag. The launch's 'launch.started' row names the instance it ran for; that instance's mirrored 'workflow.*' rows, folded oldest first
/// the way the items projection folds them, say who opened the run that is open now and whether a park, escalation or takeover holds it. The launch is
/// suspended when it is the actor that opened the open run and that run is held. A failed or attended (click) launch, a replaced run and an ended
/// instance are not suspended.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads a payload.</param>
internal sealed class LedgerRunSuspensions(ILedgerReader ledger, EventFactory events) : IRunSuspensions
{
    public ValueTask<Result<bool>> IsSuspended(LaunchId launch, CancellationToken cancellationToken)
    {
        _ = (ledger, events, launch, cancellationToken);
        throw new NotSupportedException();
    }
}
