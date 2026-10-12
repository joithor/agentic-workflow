using Aw.Kernel.Services;
using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetStatusEndpoint
{
    internal static RouteGroupBuilder MapGetStatus(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/status", static async (IService<GetStatusRequest, GetStatusResponse> service, CancellationToken ct)
            => (await service.Handle(new GetStatusRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
