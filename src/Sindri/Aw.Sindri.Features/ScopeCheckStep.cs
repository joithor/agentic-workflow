using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features;

/// <summary>'step.scope.check', on the block runtime: <see cref="ScopingSteps.Check"/>'s code behind <see cref="IBlockStep"/>.</summary>
internal sealed class ScopeCheckStep() : ScopingCodeStep(Descriptor), IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ScopingSteps.Check;
}
