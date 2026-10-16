using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.SyncSources;

internal sealed class SyncSourcesService : IService<SyncSourcesRequest, SyncReport>
{
    /// <summary>The longest one run may take before it stops and saves its cursor (spec §8.8).</summary>
    internal static readonly TimeSpan RunCap = TimeSpan.FromMinutes(2);

    public ValueTask<Result<SyncReport>> Handle(SyncSourcesRequest request, CancellationToken cancellationToken) => throw new NotSupportedException("S-5B slice 3");
}
