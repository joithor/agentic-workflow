using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'step.scope.check', on the block runtime: <c language="text">ScopingSteps.Check</c>'s code behind <see cref="IBlockStep"/>.</summary>
internal sealed class ScopeCheckStep() : ScopingCodeStep(Id), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.check");
}
