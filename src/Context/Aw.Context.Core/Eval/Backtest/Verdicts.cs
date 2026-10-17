namespace Aw.Context.Core.Eval.Backtest;

/// <summary>The words a bar or a pass line is reported with (verdicts are words, never colors).</summary>
public static class Verdicts
{
    /// <summary>The bar is met.</summary>
    public const string Met = "met";

    /// <summary>The bar is not met.</summary>
    public const string NotMet = "not met";

    /// <summary>The metric could not be measured (no data), never read as zero.</summary>
    public const string NotMeasured = "not measured";

    /// <summary>30 or more samples, every bar met.</summary>
    public const string Pass = "PASS";

    /// <summary>30 or more samples, a bar not met.</summary>
    public const string NotPassed = "NOT PASSED";

    /// <summary>Only the brief-only baseline and/or the leaky ceiling ran, so no line is drawn for the report as a whole.</summary>
    public const string NoComparableArm = "no comparable arm ran";

    /// <summary>Fewer than 30 samples: the numbers are for the record, the owner decides (spec §9.1); nothing is enabled.</summary>
    public const string OwnerDecision = "owner decision needed (small sample)";
}
