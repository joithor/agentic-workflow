namespace Aw.Context.Core.Lenses;

/// <summary>The intent band: the records that mention scope nodes and the decisions that decide them, at most 100 of each.</summary>
/// <param name="Records">Records with a 'mentions' edge to a scope node.</param>
/// <param name="Decisions">Decisions with a 'decides' edge to a scope node.</param>
/// <param name="Counts">The full counts.</param>
public sealed record IntentBand(IReadOnlyList<LensRecord> Records, IReadOnlyList<LensDecision> Decisions, IntentCounts Counts);
