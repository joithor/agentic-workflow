namespace Aw.Sindri.Host.Commands;

/// <summary>What a review loop did.</summary>
/// <param name="Accepted">Accepted.</param>
/// <param name="Rejected">Rejected.</param>
/// <param name="Edited">Renamed or moved.</param>
/// <param name="Skipped">Passed over (they stay in the queue).</param>
/// <param name="Dropped">Orphaned overrides dropped or repointed.</param>
internal sealed record ReviewCounts(int Accepted, int Rejected, int Edited, int Skipped, int Dropped);
