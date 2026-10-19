using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features;

/// <summary>
/// 'step.review.log', on the block runtime (line spec DTC4): at the round's end 'clean' (zero blocking and zero issue), 'findings', or 'behaviourWrong' (a blocking
/// finding marked behaviour: wrong), each with the running 'fact:review': the round, the count of each class, the verdict and every finding with its lens.
/// </summary>
internal sealed class ReviewLogStep() : RoundLogStep(Descriptor, LensScale.Review), IDescribedBlock
{
    private const int Blocking = 0;
    private const int Issue = 1;
    private const int Suggestion = 2;
    private const int Nit = 3;

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ReviewSteps.Log;

    /// <inheritdoc/>
    protected override BlockStepResult Ended(RoundLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        var blocking = log.At(Blocking);
        var verdict = blocking.Exists(static f => string.Equals(f["behaviour"]?.GetValue<string>(), "wrong", StringComparison.Ordinal)) ? "behaviourWrong"
            : log.Stopping().Count > 0 ? "findings"
            : "clean";
        var review = new JsonObject
        {
            ["round"] = log.Round,
            ["verdict"] = verdict,
            ["blocking"] = blocking.Count,
            ["issue"] = log.At(Issue).Count,
            ["suggestions"] = log.At(Suggestion).Count,
            ["nits"] = log.At(Nit).Count,
            ["findings"] = new JsonArray([.. log.Findings.Select(static f => f.DeepClone())]),
        };
        return Done(verdict, ("lensRounds", log.Rounds), ("review", review));
    }
}
