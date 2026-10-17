using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Syncs the intent sources into the intent layer now (context spec §8.8, API spec §4.1): a job-class command that starts a job and answers its
/// links. The same work as the 'context-sync' scheduled service, on demand, and the same 'context.sources.synced' event per source. A terminal, the
/// app and the engine's own services may start it; a session may not (the gate refuses it before the job exists).
/// </summary>
/// <param name="Source">One source's id (kebab-case), or null for every source the profile syncs.</param>
public sealed record SyncContextSourcesRequest(string? Source = null) : IJob<SyncContextSourcesResult>;
