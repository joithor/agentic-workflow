using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'step.scope.gap-log' on the block runtime: <see cref="GapLog"/> over the facts the Step reads.</summary>
internal sealed class GapLogStep : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.gap-log");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
