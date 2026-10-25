using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Extract;

/// <summary>Clusters a repo's code into candidate features (graphify, sandboxed and pinned, in Infrastructure). An absent tool is an empty, unavailable set, not an error.</summary>
public interface IClusterSource
{
    /// <summary>Clusters the repo at a commit.</summary>
    /// <param name="repo">The repo: its name and checkout.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="limits">The profile's time, snapshot, file and graph limits ('context.extract.graphify').</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The clusters.</returns>
    public ValueTask<Result<ClusterSet>> Clusters(ContextRepo repo, string commit, ClusterLimits limits, CancellationToken cancellationToken);
}
