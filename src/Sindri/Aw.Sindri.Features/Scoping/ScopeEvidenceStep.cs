using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'step.scope.evidence', on the block runtime: every check, and the packet assembled in the run area.</summary>
/// <param name="area">The instance's run area.</param>
internal sealed class ScopeEvidenceStep(IRunArea area) : ScopingCodeStep(Id), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.evidence");

    /// <inheritdoc/>
    protected override ValueTask<Result<bool>> Keep(StepContext context, ScopingDeskResponse response, CancellationToken cancellationToken)
    {
        _ = area;
        throw new NotSupportedException();
    }
}
