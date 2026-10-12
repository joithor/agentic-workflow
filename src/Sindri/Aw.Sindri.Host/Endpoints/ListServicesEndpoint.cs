using Aw.Kernel.Services;
using Aw.Sindri.Features.ListServices;

namespace Aw.Sindri.Host.Endpoints;

internal static class ListServicesEndpoint
{
    internal static RouteGroupBuilder MapListServices(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/services", static async (IService<ListServicesRequest, ListServicesResponse> service, CancellationToken ct)
            => (await service.Handle(new ListServicesRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
