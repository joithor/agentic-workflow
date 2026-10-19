using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features;

/// <summary>'step.brief.seed', on the block runtime: <see cref="ScopingSteps.BriefSeed"/>'s code behind <see cref="IBlockStep"/>.</summary>
internal sealed class BriefSeedStep() : ScopingCodeStep(Descriptor), IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ScopingSteps.BriefSeed;
}
