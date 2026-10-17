using Aw.Context.Core.Eval.MapQuality;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetContextEvalMapEndpoint
{
    internal static RouteGroupBuilder MapGetContextEvalMap(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/eval/map", static async (IService<GetContextEvalMapRequest, MapQualityRun> service, CancellationToken ct)
            => (await service.Handle(new GetContextEvalMapRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
