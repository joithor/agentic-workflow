using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Features.Prove;

/// <summary>'step.prove.before-after' on the block runtime (line spec §7.3): stub until the runtime lands.</summary>
internal sealed class ProveBeforeAfterStep(IBriefFiles files, IRunArea area, IRepoCheckouts checkouts, IWorktrees worktrees, IScriptRuns scripts) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.prove.before-after");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
