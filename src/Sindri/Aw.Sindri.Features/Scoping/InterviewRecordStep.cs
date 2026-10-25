using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'step.interview.record', on the block runtime: <c language="text">ScopingSteps.InterviewRecord</c>'s code behind <see cref="IBlockStep"/>.</summary>
internal sealed class InterviewRecordStep() : ScopingCodeStep(Id), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.interview.record");
}
