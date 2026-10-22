using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>Stub until the runtime lands.</summary>
internal sealed class RevisionBoundGate : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrGates.RevisionBound;

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
