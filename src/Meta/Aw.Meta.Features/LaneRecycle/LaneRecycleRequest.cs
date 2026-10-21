namespace Aw.Meta.Features.LaneRecycle;

/// <summary>The LaneRecycle request: clear a Claude lane's context and have it continue from its digest.</summary>
/// <param name="Lane">The lane, as named in the team config.</param>
/// <param name="Digest">The digest file the lane wrote.</param>
/// <param name="DryRun">Whether to print the steps and send nothing.</param>
public sealed record LaneRecycleRequest(string Lane, string Digest, bool DryRun);
