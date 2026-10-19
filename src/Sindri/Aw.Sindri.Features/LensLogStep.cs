using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features;

/// <summary>'step.lens.log', on the block runtime (line spec §4.1, §5.2).</summary>
internal sealed class LensLogStep : IBlockStep, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => LensRoundSteps.Log;

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
