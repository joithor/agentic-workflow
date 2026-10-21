using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Review;

/// <summary>'step.review.record' on the block runtime (line spec §7.4, DTC4): writes the review result packet to the run area and machine-freezes it through aw-packets.</summary>
/// <param name="files">Reads the phase packet's files.</param>
/// <param name="area">The instance's run area, where the packet is written.</param>
/// <param name="packets">Freezes the packet.</param>
/// <param name="checkouts">Where the repo is checked out.</param>
internal sealed class ReviewRecordStep(IBriefFiles files, IRunArea area, IPackets packets, IRepoCheckouts checkouts) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.review.record");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = (files, area, packets, checkouts);
        throw new NotSupportedException();
    }
}
