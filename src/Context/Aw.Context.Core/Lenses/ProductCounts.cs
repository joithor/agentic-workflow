namespace Aw.Context.Core.Lenses;

/// <summary>How many nodes the product band holds before the cut.</summary>
/// <param name="Own">In the own scope.</param>
/// <param name="Affects">In the cascade, outside the own scope and the neighbors.</param>
public sealed record ProductCounts(int Own, int Affects);
