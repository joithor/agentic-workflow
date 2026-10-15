using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>
/// Clusters a repo's code with graphify, pinned and sandboxed (network denied, writes confined to a private snapshot of
/// the commit). Absent, wrong version, no sandbox or a failed run is an unavailable, empty set, never an error: the build
/// goes on with deterministic anchors and says 'degraded: clusters'.
/// </summary>
/// <param name="git">Exports the commit.</param>
/// <param name="options">The pin and limits.</param>
internal sealed class GraphifyClusterSource(GitCli git, GraphifyOptions options) : IClusterSource
{
    public ValueTask<Result<ClusterSet>> Clusters(ContextRepo repo, string commit, CancellationToken cancellationToken) =>
        throw Pending.Because(git, options, typeof(GraphifyGraph), typeof(GraphifySandbox));
}
