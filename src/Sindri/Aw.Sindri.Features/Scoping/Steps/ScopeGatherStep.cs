using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features.Scoping.Steps;

/// <summary>'step.scope.gather', on the block runtime: <see cref="ScopingSteps.Gather"/>'s code behind <see cref="IBlockStep"/>.</summary>
internal sealed class ScopeGatherStep() : ScopingCodeStep(Descriptor), IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ScopingSteps.Gather;
}
