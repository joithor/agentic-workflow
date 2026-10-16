using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Features.RefreshDocs;
using Aw.Context.Features.SourcesStatus;
using Aw.Context.Features.SyncSources;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The source verbs of the facade: status, the docs refresh and the sync (kept apart so the engine stays under the constructor limit).</summary>
internal sealed class ContextSources(
    IService<SourcesStatusRequest, SourcesView> status,
    IService<RefreshDocsRequest, RefreshReport> refresh,
    IService<SyncSourcesRequest, SyncReport> sync)
{
    public ValueTask<Result<SourcesView>> Status(ContextScope scope, CancellationToken cancellationToken) =>
        status.Handle(new SourcesStatusRequest(scope), cancellationToken);

    public ValueTask<Result<RefreshReport>> Refresh(ContextScope scope, CancellationToken cancellationToken) =>
        refresh.Handle(new RefreshDocsRequest(scope), cancellationToken);

    public ValueTask<Result<SyncReport>> Sync(string? source, ContextScope scope, CancellationToken cancellationToken)
    {
        if (source is null)
        {
            return sync.Handle(new SyncSourcesRequest(scope, Source: null), cancellationToken);
        }

        return SourceId.TryFrom(source, out var id)
            ? sync.Handle(new SyncSourcesRequest(scope, id), cancellationToken)
            : ValueTask.FromResult<Result<SyncReport>>(ContextErrors.SyncSourceUnknown.With($"'{source}' isn't a source id (fix: sindri context sources status)"));
    }
}
