namespace Aw.Context.Core.Results;

/// <summary>What a sync did, one row per source it ran (spec §8.8, S-5B).</summary>
/// <param name="Sources">The sources, in run order.</param>
public sealed record SyncReport(IReadOnlyList<SourceSync> Sources);
