using Aw.Kernel.Services;
using Aw.Sindri.Features.GetProfile;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetProfileEndpoint
{
    internal static RouteGroupBuilder MapGetProfile(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/profile", static async (IService<GetProfileRequest, GetProfileResponse> service, CancellationToken ct)
            => (await service.Handle(new GetProfileRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
