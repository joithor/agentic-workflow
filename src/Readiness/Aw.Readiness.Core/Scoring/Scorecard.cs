namespace Aw.Readiness.Core.Scoring;

/// <summary>A run's levels: per pillar and overall.</summary>
/// <param name="Pillars">One score per pillar, in pillar order.</param>
/// <param name="Level">The overall level (1-5).</param>
/// <param name="Tallies">The overall tallies, one per level.</param>
public sealed record Scorecard(IReadOnlyList<PillarScore> Pillars, int Level, IReadOnlyList<LevelTally> Tallies);
