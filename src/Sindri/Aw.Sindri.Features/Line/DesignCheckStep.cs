using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>'step.design.check', on the block runtime (line spec §4.1, §4.3): the draft-stage design checks over the facts; 'pass', or 'gaps' with the findings.</summary>
internal sealed class DesignCheckStep() : LineStep(Id), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.design.check");

    /// <inheritdoc/>
    protected override IReadOnlyList<string> Structured => ["scope", "design"];

    /// <inheritdoc/>
    protected override Result<LineVerdict> Decide(LineFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var report = DesignChecks.Run(facts["scope"], facts["design"], lensRounds: null, accepted: null, evidence: false);
        return report.Passed ? LineVerdict.Of(report.Outcome) : LineVerdict.Of(report.Outcome, ("findings", report.FindingsFact()));
    }
}
