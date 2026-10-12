using Aw.Kernel.Services;
using Aw.Sindri.Features.ApproveProfile;

namespace Aw.Sindri.Host.Endpoints;

internal static class ApproveProfileEndpoint
{
    internal static RouteGroupBuilder MapApproveProfile(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/profile/approve", static async (ApproveProfileRequest request, IService<ApproveProfileRequest, ApproveProfileResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
