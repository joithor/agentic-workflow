namespace Aw.Readiness.Core.Criteria;

/// <summary>Where a criterion sits in the catalogue: its id, its pillar and the level it belongs to.</summary>
/// <param name="Id">The criterion id, such as SV-1.</param>
/// <param name="Pillar">The pillar.</param>
/// <param name="Level">The level, 1 to 5.</param>
internal sealed record CriterionKey(string Id, Pillar Pillar, int Level);
