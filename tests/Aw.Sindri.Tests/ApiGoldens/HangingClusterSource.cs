using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>A cluster source that never answers (until the host stops), so a map build stays running after it has read the repo's files (the building-in-progress golden).</summary>
internal sealed class HangingClusterSource : IClusterSource
{
    public async ValueTask<Result<ClusterSet>> Clusters(ContextRepo repo, string commit, ClusterLimits limits, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        return new ClusterSet([], Available: false);
    }
}
