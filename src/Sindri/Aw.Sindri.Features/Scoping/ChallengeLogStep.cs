using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'step.scope.challenge-log', on the block runtime: <c language="text">ScopingSteps.ChallengeLog</c>'s code behind <see cref="IBlockStep"/>.</summary>
internal sealed class ChallengeLogStep() : ScopingCodeStep(Id), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.challenge-log");
}
