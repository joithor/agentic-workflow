using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>'workflow_claims' in ledger.db (engine spec §5.9; engine P6 unit (d)).</summary>
/// <param name="database">The ledger file.</param>
/// <param name="clock">Stamps a claim.</param>
internal sealed class SqliteWorkflowClaims(LedgerDatabase database, TimeProvider clock) : IWorkflowClaims
{
    public ValueTask<Result<WorkflowClaim?>> Find(ItemKey item, CancellationToken cancellationToken)
    {
        _ = (database, clock);
        throw new NotSupportedException("engine P6 unit (d)");
    }

    public ValueTask<Result<WorkflowClaim>> Take(WorkflowClaim claim, InstanceAddress? ended, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P6 unit (d)");
}
