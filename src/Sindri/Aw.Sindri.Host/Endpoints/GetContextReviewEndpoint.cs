using Aw.Context.Core.Review;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetContextReviewEndpoint
{
    internal static RouteGroupBuilder MapGetContextReview(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/map/review", static async (int? limit, IService<GetContextReviewRequest, ReviewQueueView> service, CancellationToken ct)
            => (await service.Handle(new GetContextReviewRequest(limit), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
