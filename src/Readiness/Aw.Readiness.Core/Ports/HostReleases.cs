namespace Aw.Readiness.Core.Ports;

/// <summary>When the repo's releases were published, or unknown.</summary>
/// <param name="PublishedAt">Publication times, or null when they couldn't be read.</param>
/// <param name="Evidence">Where they came from, or why they couldn't be read.</param>
public sealed record HostReleases(IReadOnlyList<DateTimeOffset>? PublishedAt, string Evidence);
