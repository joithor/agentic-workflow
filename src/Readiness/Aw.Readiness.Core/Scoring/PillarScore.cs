namespace Aw.Readiness.Core.Scoring;

/// <summary>One pillar's level and tallies.</summary>
/// <param name="Pillar">The pillar.</param>
/// <param name="Level">Its level, or null when nothing in it was scored.</param>
/// <param name="Tallies">One tally per level, 1 to 5.</param>
public sealed record PillarScore(Pillar Pillar, int? Level, IReadOnlyList<LevelTally> Tallies);
