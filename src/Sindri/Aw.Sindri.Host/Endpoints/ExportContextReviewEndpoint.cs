using Aw.Context.Core.Review;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class ExportContextReviewEndpoint
{
    internal static RouteGroupBuilder MapExportContextReview(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/map/review/export", static async (string? path, IService<ExportContextReviewRequest, ReviewExported> service, CancellationToken ct)
            => (await service.Handle(new ExportContextReviewRequest(path), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
