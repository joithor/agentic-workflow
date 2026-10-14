namespace Aw.Sindri.Core.Events;

/// <summary>The payload of 'context.sources.synced': what one refresh changed, per repo (counts and codes only, never text).</summary>
/// <param name="Source">The source refreshed ('repo-docs').</param>
/// <param name="Slot">The scheduled slot of the run.</param>
/// <param name="Repos">Per repo.</param>
public sealed record ContextSourcesSynced(string Source, DateTimeOffset Slot, IReadOnlyList<ContextRepoSynced> Repos);
