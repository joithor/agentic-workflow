using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class PromoteContextMapEndpoint
{
    internal static RouteGroupBuilder MapPromoteContextMap(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/context/map/promote", static async (PromoteContextMapRequest request, IService<PromoteContextMapRequest, PromoteContextMapResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
