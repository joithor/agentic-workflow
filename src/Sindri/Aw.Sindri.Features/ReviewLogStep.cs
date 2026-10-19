using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features;

/// <summary>'step.review.log', on the block runtime (line spec DTC4).</summary>
internal sealed class ReviewLogStep : IBlockStep, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ReviewSteps.Log;

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
