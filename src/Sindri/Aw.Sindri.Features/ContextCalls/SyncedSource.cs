using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One source's row of a sync job.</summary>
/// <param name="Source">The source's id.</param>
/// <param name="Links">What the run did: counts and a code.</param>
public sealed record SyncedSource(string Source, ContextLinksSynced Links);
