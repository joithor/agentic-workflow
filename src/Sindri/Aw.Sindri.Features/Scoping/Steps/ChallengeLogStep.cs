using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features.Scoping.Steps;

/// <summary>'step.scope.challenge-log', on the block runtime: <see cref="ScopingSteps.ChallengeLog"/>'s code behind <see cref="IBlockStep"/>.</summary>
internal sealed class ChallengeLogStep() : ScopingCodeStep(Descriptor), IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ScopingSteps.ChallengeLog;
}
