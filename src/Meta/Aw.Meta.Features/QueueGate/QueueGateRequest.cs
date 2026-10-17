namespace Aw.Meta.Features.QueueGate;

/// <summary>The QueueGate request: gate the next pull requests of the merge queue as a train.</summary>
/// <param name="Prs">The pull requests to gate in train order; empty to take the merge queue.</param>
/// <param name="Max">The most pull requests in the train; null for the team's heavy cap.</param>
public sealed record QueueGateRequest(IReadOnlyList<int> Prs, int? Max);
