using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features;

/// <summary>'step.ship.return' on the block runtime (line spec §7.5): a red GREEN gate or a stale head goes back to build, and the findings become the notes the builder reads.</summary>
internal sealed class ShipReturnStep : IBlockStep, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ShipSteps.Return;

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
