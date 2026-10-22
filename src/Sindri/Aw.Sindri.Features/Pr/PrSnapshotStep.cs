using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>'step.pr.snapshot' on the block runtime (babysitter spec §4.3).</summary>
/// <param name="pulls">The forge, through Sindri's port.</param>
/// <param name="clock">The clock.</param>
internal sealed class PrSnapshotStep(IPullRequests pulls, TimeProvider clock) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.pr.snapshot");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
