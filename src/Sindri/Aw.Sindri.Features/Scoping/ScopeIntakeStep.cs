using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'step.scope.intake', on the block runtime: <c language="text">ScopingSteps.Intake</c>'s code behind <see cref="IBlockStep"/>.</summary>
internal sealed class ScopeIntakeStep() : ScopingCodeStep(Id), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.intake");
}
