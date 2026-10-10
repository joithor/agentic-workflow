namespace Aw.Readiness.Core.Scoring;

/// <summary>Turns findings into levels (spec §2): every repo starts at Level 1, and Level N+1 unlocks when at least 80%
/// of the scored Level N criteria pass. A level with nothing scored neither unlocks nor blocks; the first scored level
/// below 80% stops the climb.</summary>
public static class ReadinessScoring
{
    /// <summary>The highest level.</summary>
    public const int MaxLevel = 5;

    /// <summary>Scores a run's findings.</summary>
    /// <param name="findings">The findings.</param>
    /// <returns>The scorecard.</returns>
    public static Scorecard Score(IReadOnlyList<Finding> findings)
        => throw new NotSupportedException("red: ReadinessScoring.Score");
}
