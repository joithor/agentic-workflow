using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>
/// 'gate.budget.available' (spec §12, real and pure): passes while the item has tokens left to spend. Its refusal
/// parks the instance with 'budget' (§5.5), which '--grant-tokens' answers.
/// </summary>
internal sealed class BudgetAvailableGate : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("gate.budget.available"), BlockKind.Gate, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.None, Parameters: [], Rung.Deterministic, DeskCapable: false);

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: gate.budget.available");
}
