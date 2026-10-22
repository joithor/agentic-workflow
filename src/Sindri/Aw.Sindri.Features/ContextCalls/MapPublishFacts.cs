namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What <see cref="MapPublishStatusRules"/> reads: stored state and the index's commits, cut to the caller's repos.</summary>
/// <param name="HeldVersion">The held draft's number, or null when nothing is held.</param>
/// <param name="Since">When the hold's wait began (held only).</param>
/// <param name="Wait">The profile's wait for the index (held only).</param>
/// <param name="Current">The current version's number, or null.</param>
/// <param name="Repos">The caller's repos.</param>
/// <param name="Reason">The 'published' event's reason for the current version, or null.</param>
/// <param name="ByHand">True when a human promoted the current version.</param>
/// <param name="PublishedAt">When the current version's 'published' event was recorded, or null.</param>
/// <param name="DroppedVersion">The held map a newer build replaced, when that drop is the newest map event; otherwise null.</param>
internal sealed record MapPublishFacts(int? HeldVersion, DateTimeOffset Since, TimeSpan Wait, int? Current, IReadOnlyList<PublishRepoFact> Repos, string? Reason, bool ByHand, DateTimeOffset? PublishedAt = null, int? DroppedVersion = null);
