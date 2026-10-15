namespace Aw.Context.Core.Lenses;

/// <summary>How many records and decisions the intent band holds before the cut.</summary>
/// <param name="Records">Records.</param>
/// <param name="Decisions">Decisions.</param>
/// <param name="Orphaned">Orphaned links onto the scope; null while nothing computes it (S-4).</param>
public sealed record IntentCounts(int Records, int Decisions, int? Orphaned);
