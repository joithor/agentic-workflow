using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Build;

/// <summary>
/// 'step.phase.seed' on the block runtime (line spec §7.1): creates the phase's branch <c language="text">sindri/&lt;line&gt;/&lt;phase&gt;</c> and its worktree at the base the line chose, only
/// when the instance holds no 'fact:workspace', and seeds the round-1 value of every fact a later wait reads. SND-WT-002 (the path or branch is in use at another base) stops the Step with
/// nothing reset, so the instance parks for the owner; SND-WT-003 (git failed) fails it.
/// </summary>
/// <param name="worktrees">Makes the worktree.</param>
/// <param name="checkouts">Where the repo is checked out.</param>
internal sealed class PhaseSeedStep(IWorktrees worktrees, IRepoCheckouts checkouts) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.phase.seed");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException($"{worktrees} {checkouts}");
}
