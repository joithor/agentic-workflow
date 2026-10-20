using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The 'scoping.approved' row the engine appends when 'step.scope.freeze' reports 'frozen'. Not built yet.</summary>
/// <param name="ledger">The ledger.</param>
/// <param name="lease">The lease this process holds.</param>
/// <param name="events">The event factory.</param>
/// <param name="area">The instance's run area.</param>
internal sealed class ScopingApprovals(ILedgerStore ledger, HeldLease lease, EventFactory events, IRunArea area)
{
    /// <summary>The Step whose 'frozen' report is an approval.</summary>
    public static BlockId Freeze { get; } = BlockId.From("step.scope.freeze");

    /// <summary>Appends the row when <paramref name="result"/> is the freeze Step's 'frozen'.</summary>
    /// <param name="step">The Step that ran.</param>
    /// <param name="context">What it ran with.</param>
    /// <param name="result">What it returned.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The result, or a failed one when the row could not be appended.</returns>
    public ValueTask<BlockStepResult> After(BlockId step, StepContext context, BlockStepResult result, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{step.Value} {context.Step.Value} {result.Failure} {ledger.GetType().Name} {lease.GetType().Name} {events.GetType().Name} {area.GetType().Name}");
}
