using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.LensLoops;

/// <summary>'step.review.log', on the block runtime (line spec §7.4, DTC4): at the round's end the running 'fact:review' and clean, findings, behaviourWrong or needsOwner.</summary>
internal sealed class ReviewLogStep() : RoundLogStep(LensScale.Review), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.review.log");

    /// <inheritdoc/>
    protected override BlockStepResult Ended(RoundLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _ = Escalation.First(log.Stopping());
        throw new NotSupportedException();
    }
}
