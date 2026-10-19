using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features.Scoping.Steps;

/// <summary>'step.scope.intake', on the block runtime: <see cref="ScopingSteps.Intake"/>'s code behind <see cref="IBlockStep"/>.</summary>
internal sealed class ScopeIntakeStep() : ScopingCodeStep(Descriptor), IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ScopingSteps.Intake;
}
