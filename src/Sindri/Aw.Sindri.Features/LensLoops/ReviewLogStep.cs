using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.LensLoops;

/// <summary>
/// 'step.review.log', on the block runtime (line spec §7.4, DTC4): at the round's end the running 'fact:review' and one of four outcomes. 'behaviourWrong' when a blocking finding says the
/// behaviour is wrong; else 'needsOwner' when a blocking or issue finding carries a typed escalation (with 'fact:escalation'); else 'findings' when any blocking or issue finding is left; else 'clean'.
/// The loop asks a human only through a typed escalation (<see cref="Escalation"/>); a cap or no progress is the recipe's route, never this Step's.
/// </summary>
internal sealed class ReviewLogStep() : RoundLogStep(LensScale.Review), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.review.log");

    /// <inheritdoc/>
    protected override BlockStepResult Ended(RoundLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        var stopping = log.Stopping();
        var wrong = log.At(0).Exists(static f => LoopJson.Str(f, "behaviour") == "wrong");
        var escalation = wrong ? null : Escalation.First(stopping);
        var verdict = wrong ? "behaviourWrong" : stopping.Count > 0 ? "findings" : "clean";
        var review = Review(log, verdict, wrong);
        return escalation is null
            ? Done(verdict, ("lensRounds", log.Rounds), ("review", review))
            : Done("needsOwner", ("lensRounds", log.Rounds), ("review", review), ("escalation", escalation));
    }

    private static JsonObject Review(RoundLog log, string verdict, bool wrong)
    {
        var review = new JsonObject
        {
            ["round"] = log.Round,
            ["verdict"] = verdict,
            ["blocking"] = log.At(0).Count,
            ["issue"] = log.At(1).Count,
            ["suggestions"] = log.At(2).Count,
            ["nits"] = log.At(3).Count,
        };
        if (wrong)
        {
            review["behaviour"] = "wrong";
        }

        review["findings"] = new JsonArray([.. log.Findings.Select(static f => (JsonNode?)f.DeepClone())]);
        return review;
    }
}
