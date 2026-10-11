namespace Aw.Judge.Features.PruneDecisionDetails;

/// <summary>The PruneDecisionDetails response.</summary>
/// <param name="Pruned">How many detail rows were deleted.</param>
public sealed record PruneDecisionDetailsResponse(int Pruned);
