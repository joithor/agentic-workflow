using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Launches;

/// <summary>What an agent has spent today (screens spec E-28): the read port OpenItem's budget check uses. The usage readers implement it (API P10a unit 11).</summary>
public interface ILaunchSpend
{
    /// <summary>The agent's spend today in USD, in the local day.</summary>
    /// <param name="target">The agent.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The amount, or null when the agent has no usage reader.</returns>
    public ValueTask<Result<decimal?>> Today(AgentId target, CancellationToken cancellationToken);
}
