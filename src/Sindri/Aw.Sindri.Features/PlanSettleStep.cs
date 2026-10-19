using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Line;

namespace Aw.Sindri.Features;

/// <summary>'step.plan.settle', on the block runtime (line spec §6.1): compares the latest answer batch with the design's assumptions; 'assumed', 'changed' or 'replan'.</summary>
internal sealed class PlanSettleStep() : LineStep(Descriptor), IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PlanSteps.Settle;

    /// <inheritdoc/>
    protected override Result<LineVerdict> Decide(LineFacts facts) => throw new NotSupportedException();
}
