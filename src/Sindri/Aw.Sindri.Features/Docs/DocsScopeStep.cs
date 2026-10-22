using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Features.Docs;

/// <summary>'step.docs.scope' on the block runtime (docs-sync spec §7.2, W28); the stub of the red commit.</summary>
internal sealed class DocsScopeStep(IRepoCheckouts checkouts, IWorktrees worktrees, IChangedPaths changed, ITrackedFiles tracked, IBriefFiles files) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.docs.scope");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
