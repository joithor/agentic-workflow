using Aw.Readiness.Core;
using Aw.Readiness.Core.Scoring;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.Scoring;

public sealed class ReadinessScoringTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_repo_with_nothing_scored_is_level_1_and_every_pillar_has_no_level()
    {
        var card = ReadinessScoring.Score([A.Finding("SV-1", CriterionState.Unknown)]);

        Assert.Equal(1, card.Level);
        Assert.All(card.Pillars, p => Assert.Null(p.Level));
    }

    [Fact, Trait("Lane", "unit")]
    public void Eighty_percent_of_a_level_unlocks_the_next()
    {
        Finding[] findings = [
            A.Finding("TS-1", CriterionState.Pass), A.Finding("TS-2", CriterionState.Pass),
            A.Finding("TS-3", CriterionState.Pass), A.Finding("TS-4", CriterionState.Pass),
            A.Finding("TS-5", CriterionState.Fail),
        ];

        var card = ReadinessScoring.Score(findings);

        Assert.Equal(2, card.Level);
        Assert.Equal(new LevelTally(1, 4, 5), card.Tallies[0]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Below_eighty_percent_stops_the_climb_even_when_higher_levels_pass()
    {
        Finding[] findings = [
            A.Finding("TS-1", CriterionState.Pass), A.Finding("TS-2", CriterionState.Pass),
            A.Finding("TS-3", CriterionState.Pass, level: 2), A.Finding("TS-4", CriterionState.Fail, level: 2),
            A.Finding("TS-5", CriterionState.Pass, level: 3),
        ];

        Assert.Equal(2, ReadinessScoring.Score(findings).Level);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_level_neither_unlocks_nor_blocks()
    {
        // Level 2 is empty: Level 1 alone reaches 2; a passing Level 3 then reaches 4.
        Finding[] onlyLevel1 = [A.Finding("TS-1", CriterionState.Pass)];
        Finding[] withLevel3 = [A.Finding("TS-1", CriterionState.Pass), A.Finding("TS-2", CriterionState.Pass, level: 3)];

        Assert.Equal(2, ReadinessScoring.Score(onlyLevel1).Level);
        Assert.Equal(4, ReadinessScoring.Score(withLevel3).Level);
    }

    [Fact, Trait("Lane", "unit")]
    public void Unknown_and_not_applicable_are_not_scored()
    {
        Finding[] findings = [
            A.Finding("TS-1", CriterionState.Pass), A.Finding("TS-2", CriterionState.Unknown),
            A.Finding("TS-3", CriterionState.NotApplicable),
        ];

        var card = ReadinessScoring.Score(findings);

        Assert.Equal(new LevelTally(1, 1, 1), card.Tallies[0]);
        Assert.Equal(2, card.Level);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_level_passing_reaches_level_5_and_no_further()
    {
        var findings = Enumerable.Range(1, 5).Select(l => A.Finding("TS-" + l.ToString(System.Globalization.CultureInfo.InvariantCulture), CriterionState.Pass, level: l)).ToList();

        Assert.Equal(ReadinessScoring.MaxLevel, ReadinessScoring.Score(findings).Level);
    }

    [Fact, Trait("Lane", "unit")]
    public void Pillars_are_scored_on_their_own_findings_in_pillar_order()
    {
        Finding[] findings = [
            A.Finding("SV-1", CriterionState.Fail, Pillar.StyleAndValidation),
            A.Finding("TS-1", CriterionState.Pass, Pillar.Testing),
        ];

        var card = ReadinessScoring.Score(findings);

        Assert.Equal(Enum.GetValues<Pillar>(), card.Pillars.Select(p => p.Pillar));
        Assert.Equal(1, card.Pillars.Single(p => p.Pillar == Pillar.StyleAndValidation).Level);
        Assert.Equal(2, card.Pillars.Single(p => p.Pillar == Pillar.Testing).Level);
        Assert.Null(card.Pillars.Single(p => p.Pillar == Pillar.Security).Level);
        Assert.Equal(5, card.Pillars[0].Tallies.Count);
    }
}
