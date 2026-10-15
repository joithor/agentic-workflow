using Aw.Kernel.Services;
using Aw.Sindri.Features.GetItemTimeline;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetItemTimelineEndpoint
{
    internal static RouteGroupBuilder MapGetItemTimeline(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/items/{itemId}/timeline", static async (string itemId, long? after, IService<GetItemTimelineRequest, TimelineResponse> service, CancellationToken ct) =>
        {
            var item = ItemRoute.Item(itemId);
            return item.IsError ? item.ToHttpResult() : (await service.Handle(new GetItemTimelineRequest(item.Value, after), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
