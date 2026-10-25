using Aw.Kernel.Services;
using Aw.Sindri.Features.GetSetup;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetSetupEndpoint
{
    internal static RouteGroupBuilder MapGetSetup(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/setup", static async (IService<GetSetupRequest, GetSetupResponse> service, CancellationToken ct)
            => (await service.Handle(new GetSetupRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
