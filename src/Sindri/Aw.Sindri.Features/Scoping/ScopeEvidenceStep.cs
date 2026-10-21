using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.evidence', on the block runtime (scoping spec §8.2, SC7): <c language="text">ScopingSteps.Evidence</c>'s code behind <see cref="IBlockStep"/>. Every check runs over the facts,
/// and on 'complete' the packet is assembled in the instance's run area as the desk assembles it, so the owner's stop reads files, not a claim. A run area that cannot be written fails the Step.
/// </summary>
/// <param name="area">The instance's run area.</param>
internal sealed class ScopeEvidenceStep(IRunArea area) : ScopingCodeStep(Id), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.evidence");

    /// <inheritdoc/>
    protected override ValueTask<Result<bool>> Keep(StepContext context, ScopingDeskResponse response, CancellationToken cancellationToken) =>
        WriteFiles(area, context, response, cancellationToken);
}
