using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>'step.design.check', on the block runtime (line spec §4.1, §4.3): the draft-stage design checks over the facts, spec.md against the style pack 'fact:packs' pins (DSN-STYLE-001); 'pass', or 'gaps' with the findings.</summary>
/// <param name="styles">The style packs, to read the pinned one again.</param>
internal sealed class DesignCheckStep(IStylePacks styles) : LineStep(Id), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.design.check");

    /// <inheritdoc/>
    protected override IReadOnlyList<string> Structured => ["scope", "design"];

    /// <inheritdoc/>
    protected override Result<LineVerdict> Decide(LineFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var pinned = StylePin.Read(styles, facts["packs"]);
        if (pinned.Failure is { } failure)
        {
            return SindriErrors.InvalidInput.With(failure);
        }

        var report = DesignChecks.RunStyled(facts["scope"], facts["design"], (Rounds: null, Accepted: null), evidence: false, pinned.Pack);
        return report.Passed ? LineVerdict.Of(report.Outcome) : LineVerdict.Of(report.Outcome, ("findings", report.FindingsFact()));
    }
}
