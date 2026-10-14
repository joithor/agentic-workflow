using Aw.Context.Core.Map;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetContextMapEndpoint
{
    internal static RouteGroupBuilder MapGetContextMap(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/map", static async (IService<GetContextMapRequest, MapStatusView> service, CancellationToken ct)
            => (await service.Handle(new GetContextMapRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
