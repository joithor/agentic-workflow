using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// Who holds an item's claim (port; engine spec §5.7: 'effect.claim.ref' keys its identity by item through
/// 'workflow_claims' in ledger.db; engine P6 unit (d)). The ref 'sindri/{user}/{item}' is only a marker.
/// </summary>
public interface IWorkflowClaims
{
    /// <summary>Records <paramref name="claim"/> unless another instance holds the item.</summary>
    /// <param name="claim">The claim to take.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The claim that holds the item afterwards: <paramref name="claim"/> itself (taken now or before), or another instance's ('claimExists').</returns>
    public ValueTask<Result<WorkflowClaim>> Take(WorkflowClaim claim, CancellationToken cancellationToken);
}
