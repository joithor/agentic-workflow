using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Testing.Extract;

/// <summary>An <see cref="IClusterSource"/> that answers a fixed <see cref="Set"/> (unavailable by default, like a machine without graphify) and records what it was asked.</summary>
public sealed class FakeClusterSource : IClusterSource
{
    /// <summary>What every call answers.</summary>
    public ClusterSet Set { get; set; } = new([], Available: false);

    /// <summary>The repos asked about, in order.</summary>
    public IList<string> Asked { get; } = [];

    /// <inheritdoc/>
    public ValueTask<Result<ClusterSet>> Clusters(ContextRepo repo, string commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        Asked.Add($"{repo.Name.Value}@{commit}");
        return ValueTask.FromResult<Result<ClusterSet>>(Set);
    }
}
