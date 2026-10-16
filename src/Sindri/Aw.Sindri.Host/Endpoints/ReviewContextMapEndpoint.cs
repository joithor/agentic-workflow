using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class ReviewContextMapEndpoint
{
    internal static RouteGroupBuilder MapReviewContextMap(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/context/map/review", static async (ReviewContextMapRequest request, IService<ReviewContextMapRequest, ReviewContextMapResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
