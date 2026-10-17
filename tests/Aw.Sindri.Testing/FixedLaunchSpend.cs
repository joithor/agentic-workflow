using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Testing;

/// <summary>A spend reader with set figures: an agent not set has no usage reader.</summary>
public sealed class FixedLaunchSpend : ILaunchSpend
{
    private readonly Dictionary<AgentId, decimal> _spent = [];

    /// <summary>Sets what <paramref name="target"/> has spent today.</summary>
    /// <param name="target">The agent.</param>
    /// <param name="usd">The amount in USD.</param>
    public void Spent(AgentId target, decimal usd) => _spent[target] = usd;

    /// <inheritdoc/>
    public ValueTask<Result<decimal?>> Today(AgentId target, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(_spent.TryGetValue(target, out var usd) ? (decimal?)usd : null));
}
