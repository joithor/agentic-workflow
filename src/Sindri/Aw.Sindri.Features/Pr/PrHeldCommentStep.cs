using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>'step.pr.held-comment' on the block runtime (babysitter spec §5.3): the oldest comment marked 'held' before the owner's walk started, as 'fact:comment'; an empty walk is 'drainedUnpushed' when the workspace holds unpushed commits, else 'drained'.</summary>
internal sealed class PrHeldCommentStep : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.pr.held-comment");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}
