using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>'step.plan.check', on the block runtime (line spec §6.1, §6.3): the draft-stage plan checks over the facts; 'pass', or 'gaps' with the findings.</summary>
internal sealed class PlanCheckStep() : LineStep(Id), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.plan.check");

    /// <inheritdoc/>
    protected override IReadOnlyList<string> Structured => ["design", "plan"];

    /// <inheritdoc/>
    protected override Result<LineVerdict> Decide(LineFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var report = PlanChecks.Run(facts["design"], facts["plan"], holdoutSets: null, evidence: false);
        return report.Passed ? LineVerdict.Of(report.Outcome) : LineVerdict.Of(report.Outcome, ("findings", report.FindingsFact()));
    }
}
