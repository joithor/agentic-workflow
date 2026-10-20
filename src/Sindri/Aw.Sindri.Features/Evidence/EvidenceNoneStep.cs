using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Evidence;

/// <summary>'step.evidence.none' on the block runtime (decision-tree spec §3.3): nothing needs evidence beyond the always-run checks, and the summary says so.</summary>
internal sealed class EvidenceNoneStep : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.evidence.none");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
