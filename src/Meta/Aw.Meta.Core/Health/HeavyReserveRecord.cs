namespace Aw.Meta.Core.Health;

/// <summary>One computation of the heavy-slot reservation, as kept in the team's state directory.</summary>
/// <param name="Reserve">The slots the meta gates should hold.</param>
/// <param name="Depth">The merge-queue depth it was computed from.</param>
/// <param name="At">When it was computed.</param>
public sealed record HeavyReserveRecord(int Reserve, int Depth, DateTimeOffset At);
