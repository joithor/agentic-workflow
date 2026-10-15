using Aw.Context.Core.Review;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetContextReviewHistoryEndpoint
{
    internal static RouteGroupBuilder MapGetContextReviewHistory(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/map/review/history", static async (int? limit, IService<GetContextReviewHistoryRequest, ReviewHistoryView> service, CancellationToken ct)
            => (await service.Handle(new GetContextReviewHistoryRequest(limit), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
