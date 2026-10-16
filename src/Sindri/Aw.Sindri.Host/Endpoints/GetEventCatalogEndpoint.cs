using Aw.Kernel.Services;
using Aw.Sindri.Features.GetEventCatalog;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetEventCatalogEndpoint
{
    internal static RouteGroupBuilder MapGetEventCatalog(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/events/catalog", static async (IService<GetEventCatalogRequest, EventCatalogView> service, CancellationToken ct)
            => (await service.Handle(new GetEventCatalogRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
