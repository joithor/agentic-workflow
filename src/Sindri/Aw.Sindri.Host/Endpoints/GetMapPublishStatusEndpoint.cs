using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetMapPublishStatusEndpoint
{
    internal static RouteGroupBuilder MapGetMapPublishStatus(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/map/publish", static async (IService<GetMapPublishStatusRequest, MapPublishStatusView> service, CancellationToken ct)
            => (await service.Handle(new GetMapPublishStatusRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
