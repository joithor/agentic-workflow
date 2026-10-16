namespace Aw.Sindri.Core.Events;

/// <summary>The payload of 'context.sources.synced': what one refresh changed, per repo (counts and codes only, never text).</summary>
/// <param name="Source">The source refreshed ('repo-docs').</param>
/// <param name="Slot">The scheduled slot of the run.</param>
/// <param name="Repos">Per repo (a docs refresh; none for an intent source).</param>
/// <param name="Links">What a sync of an intent source did (null for a docs refresh).</param>
public sealed record ContextSourcesSynced(string Source, DateTimeOffset Slot, IReadOnlyList<ContextRepoSynced> Repos, ContextLinksSynced? Links = null);
