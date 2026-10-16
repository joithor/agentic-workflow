namespace Aw.Context.Core.Eval.Backtest;

/// <summary>The §9.4 pass line for one arm (spec §9.1, §9.4). Pure.</summary>
public static class PassLine
{
    /// <summary>Judges an arm.</summary>
    /// <param name="arm">The arm's means.</param>
    /// <param name="brief">The brief-only arm's means, or null when it did not run (then "above brief-only" is not measured). Pass null for the brief arm itself.</param>
    /// <param name="sampleCount">The corpus size: below 30 the overall line is the owner decision whatever the bars say.</param>
    /// <returns>The bars and the overall line.</returns>
    public static ArmPassLine Evaluate(ArmScore arm, ArmScore? brief, int sampleCount)
    {
        ArgumentNullException.ThrowIfNull(arm);
        var recall = Recall(arm, brief);
        var precision = AtLeast(arm.NodePrecision, PassBars.NodePrecision);
        var files = AtLeast(arm.FileRecallAt20, PassBars.FileRecallAt20);
        var tokens = arm.Tokens <= PassBars.MaxTokens ? Verdicts.Met : Verdicts.NotMet;
        string[] bars = [recall, precision, files, tokens];
        var overall = sampleCount < PassBars.MinSamples
            ? Verdicts.OwnerDecision
            : bars.Contains(Verdicts.NotMet) ? Verdicts.NotPassed : bars.Contains(Verdicts.NotMeasured) ? Verdicts.NotMeasured : Verdicts.Pass;
        return new ArmPassLine(recall, precision, files, tokens, overall);
    }

    private static string Recall(ArmScore arm, ArmScore? brief)
    {
        var floor = AtLeast(arm.NodeRecall, PassBars.NodeRecall);
        if (brief is null)
        {
            return floor;
        }

        var above = arm.NodeRecall is { } r && brief.NodeRecall is { } b ? (r > b ? Verdicts.Met : Verdicts.NotMet) : Verdicts.NotMeasured;
        return floor == Verdicts.NotMet || above == Verdicts.NotMet ? Verdicts.NotMet : floor == Verdicts.Met && above == Verdicts.Met ? Verdicts.Met : Verdicts.NotMeasured;
    }

    private static string AtLeast(double? value, double bar) => value is null ? Verdicts.NotMeasured : value >= bar ? Verdicts.Met : Verdicts.NotMet;
}
