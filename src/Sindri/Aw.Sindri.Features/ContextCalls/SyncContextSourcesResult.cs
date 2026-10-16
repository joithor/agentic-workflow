namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a sync job did, small by design (the job's result is capped at 16 KiB): per source its counts and the code it ended with, never record text.</summary>
/// <param name="Sources">One row per source synced.</param>
public sealed record SyncContextSourcesResult(IReadOnlyList<SyncedSource> Sources);
