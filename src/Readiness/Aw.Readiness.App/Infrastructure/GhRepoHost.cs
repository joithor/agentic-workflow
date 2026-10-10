using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.App.Infrastructure;

/// <summary>The gh CLI adapter of <see cref="IRepoHost"/>: read-only 'gh repo view' and 'gh api' GETs.</summary>
/// <param name="settings">Which gh to run and its timeout.</param>
internal sealed class GhRepoHost(ReadinessSettings settings) : IRepoHost
{
    public ValueTask<HostFacts> Read(string root, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"red: GhRepoHost over {settings.GhBinary}");
}
