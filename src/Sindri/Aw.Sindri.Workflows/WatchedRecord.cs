namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One watched pull request.</summary>
/// <param name="Digest">The digest read last.</param>
/// <param name="ActiveAt">When the pull request last changed (or was first seen), the start of its quiet period.</param>
/// <param name="IdleAt">When the last 'idle' was sent, if one was in this quiet period.</param>
internal sealed record WatchedRecord(WatchedDigest Digest, DateTimeOffset ActiveAt, DateTimeOffset? IdleAt);
