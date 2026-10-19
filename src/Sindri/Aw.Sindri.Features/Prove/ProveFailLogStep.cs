using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

/// <summary>'step.prove.fail-log' on the block runtime (line spec §7.3): every failure path ends here and the findings say what failed.</summary>
internal sealed class ProveFailLogStep : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.prove.fail-log");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException("red");
}
