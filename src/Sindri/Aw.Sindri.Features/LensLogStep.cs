using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features;

/// <summary>
/// 'step.lens.log', on the block runtime (line spec §4.1): at the round's end 'clean' (0 CRITICAL, 0 HIGH), else 'findings' with those findings as 'fact:findings'.
/// </summary>
internal sealed class LensLogStep() : RoundLogStep(Descriptor, LensScale.Design), IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => LensRoundSteps.Log;

    /// <inheritdoc/>
    protected override BlockStepResult Ended(RoundLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        return log.Stopping() is { Count: > 0 } stopping
            ? Done("findings", ("lensRounds", log.Rounds), ("findings", new JsonArray([.. stopping])))
            : Done("clean", ("lensRounds", log.Rounds));
    }
}
