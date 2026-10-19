using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'step.brief.seed', on the block runtime: <c language="text">ScopingSteps.BriefSeed</c>'s code behind <see cref="IBlockStep"/>.</summary>
internal sealed class BriefSeedStep() : ScopingCodeStep(Id), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.brief.seed");
}
