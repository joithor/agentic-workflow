using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class ProposeContextMapEndpoint
{
    internal static RouteGroupBuilder MapProposeContextMap(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/context/map/proposals", static async (ProposeContextMapRequest request, IService<ProposeContextMapRequest, ProposeContextMapResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
