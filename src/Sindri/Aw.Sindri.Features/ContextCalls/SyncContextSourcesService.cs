using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The work of a sync job: syncs through the engine under the starting caller and records 'context.sources.synced' per source.</summary>
internal sealed class SyncContextSourcesService : IService<SyncContextSourcesRequest, SyncContextSourcesResult>
{
    public ValueTask<Result<SyncContextSourcesResult>> Handle(SyncContextSourcesRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
