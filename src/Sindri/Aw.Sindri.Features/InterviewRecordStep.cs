using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features;

/// <summary>'step.interview.record', on the block runtime: <see cref="ScopingSteps.InterviewRecord"/>'s code behind <see cref="IBlockStep"/>.</summary>
internal sealed class InterviewRecordStep() : ScopingCodeStep(Descriptor), IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ScopingSteps.InterviewRecord;
}
