using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// 'step.pr.next-comment' on the block runtime (babysitter spec §5.2): the oldest unhandled thread of the snapshot's queue, as 'fact:comment'. Authorship is checked before
/// content: 'untrusted', then 'bot', then 'comment'; an empty queue is 'drainedUnpushed' when the workspace holds commits the PR head lacks, else 'drained'.
/// </summary>
internal sealed class PrNextCommentStep : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.pr.next-comment");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}
