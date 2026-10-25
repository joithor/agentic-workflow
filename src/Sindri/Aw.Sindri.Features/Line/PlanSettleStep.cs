using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>'step.plan.settle', on the block runtime (line spec §6.1): compares the latest answer batch with the design's assumptions; 'assumed', 'changed' or 'replan'.</summary>
internal sealed class PlanSettleStep() : LineStep(Id), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.plan.settle");

    /// <inheritdoc/>
    protected override IReadOnlyList<string> Structured => ["design", "answers"];

    /// <inheritdoc/>
    protected override Result<LineVerdict> Decide(LineFacts facts) => PlanSettle.Run(facts["design"], facts["answers"]);
}
