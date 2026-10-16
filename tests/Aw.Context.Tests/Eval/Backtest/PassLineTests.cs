using Aw.Context.Core.Eval.Backtest;

namespace Aw.Context.Tests.Eval.Backtest;

public sealed class PassLineTests
{
    private static ArmScore Arm(double? recall, double? precision, double? files, int tokens = 0) => new(40, 40, recall, precision, files, 0.0, tokens);

    [Fact, Trait("Lane", "unit")]
    public void Pass_line_below_30_samples_is_owner_decision_and_enables_nothing()
    {
        var line = PassLine.Evaluate(Arm(0.9, 0.9, 0.9), Arm(0.1, 0.1, 0.1), sampleCount: 29);

        Assert.Equal(Verdicts.OwnerDecision, line.Overall);
        Assert.Equal((Verdicts.Met, Verdicts.Met, Verdicts.Met, Verdicts.Met), (line.NodeRecall, line.NodePrecision, line.FileRecallAt20, line.Tokens));   // measured for the record
    }

    [Fact, Trait("Lane", "unit")]
    public void Pass_line_at_30_or_more_is_met_only_when_recall_beats_brief_only()
    {
        var beats = PassLine.Evaluate(Arm(0.7, 0.7, 0.6), Arm(0.5, 0.2, 0.2), sampleCount: 30);
        var ties = PassLine.Evaluate(Arm(0.7, 0.7, 0.6), Arm(0.7, 0.2, 0.2), sampleCount: 30);

        Assert.Equal(Verdicts.Pass, beats.Overall);
        Assert.Equal((Verdicts.NotMet, Verdicts.NotPassed), (ties.NodeRecall, ties.Overall));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_missed_bar_is_not_passed_and_an_unmeasured_one_is_not_measured()
    {
        Assert.Equal(Verdicts.NotPassed, PassLine.Evaluate(Arm(0.7, 0.59, 0.6), brief: null, sampleCount: 40).Overall);
        Assert.Equal(Verdicts.NotPassed, PassLine.Evaluate(Arm(0.7, 0.7, 0.6, tokens: 1_500_001), brief: null, sampleCount: 40).Overall);
        var unmeasured = PassLine.Evaluate(Arm(recall: null, precision: null, files: 0.6), brief: null, sampleCount: 40);
        Assert.Equal((Verdicts.NotMeasured, Verdicts.NotMeasured), (unmeasured.NodeRecall, unmeasured.Overall));
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_brief_only_arm_above_brief_only_is_not_measured_for_an_arm_that_needs_it()
    {
        // an arm that is judged against brief-only, when brief did not run, can't claim the bar
        var line = PassLine.Evaluate(Arm(0.9, 0.9, 0.9), Arm(recall: null, precision: null, files: null), sampleCount: 40);

        Assert.Equal(Verdicts.NotMeasured, line.NodeRecall);
    }
}
