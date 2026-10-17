using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The 'context-purge' scheduled service (context spec §6.1, §9.1): drops map exports past 14 days and map versions past the last 20, and eval reports and the as-of map cache past 90 days.</summary>
internal sealed class ContextPurgeService(IContextEngine engine) : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("context-purge");

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var purged = await engine.PurgeExpired(cancellationToken).ConfigureAwait(false);
        if (purged.IsError)
        {
            return purged.Error;
        }

        var scope = new ContextScope(Settings: null, [], new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service));
        var eval = await engine.EvalPurge(new EvalPurgeRequest(scope, All: false), cancellationToken).ConfigureAwait(false);
        return eval.IsError ? eval.Error : ScheduledResult.Worked;
    }
}
