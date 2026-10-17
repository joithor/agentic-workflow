using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.OpenItem;

/// <summary>The spend reader until the agents' usage readers exist (API P10a unit 11): no agent has one, so a budgeted agent is refused as SND-LAUNCH-010 'no usage reader' and an unbudgeted one is unaffected.</summary>
internal sealed class NoLaunchSpend : ILaunchSpend
{
    public ValueTask<Result<decimal?>> Today(AgentId target, CancellationToken cancellationToken) => ValueTask.FromResult<Result<decimal?>>((decimal?)null);
}
