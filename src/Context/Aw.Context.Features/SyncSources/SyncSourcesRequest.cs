using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;

namespace Aw.Context.Features.SyncSources;

/// <summary>Sync one source, or every source when <paramref name="Source"/> is null.</summary>
/// <param name="Scope">The caller's scope.</param>
/// <param name="Source">The source to sync, or null for all.</param>
internal sealed record SyncSourcesRequest(ContextScope Scope, SourceId? Source);
