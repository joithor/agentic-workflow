namespace Aw.Readiness.Core.Scoring;

/// <summary>Turns findings into levels (spec §2): every repo starts at Level 1, and Level N+1 unlocks when at least 80%
/// of the scored Level N criteria pass. A level with nothing scored neither unlocks nor blocks: the climb skips it, so
/// the level is one above the highest scored level that passed with every scored level below it passing (the R0
/// baseline's rule). The first scored level below 80% stops the climb, so with L1 passing, L2 empty and L3 failing the
/// level is 2: Level 3 is never awarded on an empty Level 2.</summary>
public static class ReadinessScoring
{
    /// <summary>The highest level.</summary>
    public const int MaxLevel = 5;

    /// <summary>Scores a run's findings.</summary>
    /// <param name="findings">The findings.</param>
    /// <returns>The scorecard.</returns>
    public static Scorecard Score(IReadOnlyList<Finding> findings)
    {
        var pillars = Enum.GetValues<Pillar>()
            .Select(pillar => PillarOf(pillar, [.. findings.Where(f => f.Criterion.Pillar == pillar)]))
            .ToList();
        var tallies = Tally(findings);
        return new Scorecard(pillars, LevelOf(tallies), tallies);
    }

    private static PillarScore PillarOf(Pillar pillar, IReadOnlyList<Finding> findings)
    {
        var tallies = Tally(findings);
        return new PillarScore(pillar, tallies.TrueForAll(t => t.IsEmpty) ? null : LevelOf(tallies), tallies);
    }

    private static List<LevelTally> Tally(IReadOnlyList<Finding> findings) =>
        [.. Enumerable.Range(1, MaxLevel).Select(level =>
        {
            var scored = findings.Where(f => f.Criterion.Level == level && IsScored(f.State)).ToList();
            return new LevelTally(level, scored.Count(f => f.State == CriterionState.Pass), scored.Count);
        })];

    private static int LevelOf(IReadOnlyList<LevelTally> tallies)
    {
        var level = 1;
        foreach (var tally in tallies.Where(t => t.Level < MaxLevel && !t.IsEmpty))
        {
            if (!tally.Unlocks)
            {
                break;
            }

            level = tally.Level + 1;
        }

        return level;
    }

    private static bool IsScored(CriterionState state) => state is CriterionState.Pass or CriterionState.Fail;
}
