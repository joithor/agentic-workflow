using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>The spend reader (stub until its tests pass): an agent's usage today, in the local day, priced.</summary>
/// <param name="catalog">The catalog, which names each entry's reader.</param>
/// <param name="readers">The readers by name.</param>
/// <param name="clock">The time and the local zone.</param>
internal sealed class UsageSpend(IAgentCatalog catalog, IReadOnlyDictionary<string, IUsageReader> readers, TimeProvider clock) : ILaunchSpend
{
    /// <inheritdoc/>
    public ValueTask<Result<AgentSpend?>> Today(AgentId target, CancellationToken cancellationToken) => throw new NotSupportedException($"{catalog}{readers}{clock}");
}
