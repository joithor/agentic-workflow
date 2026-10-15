using Aw.Kernel.Services;
using Aw.Sindri.Features.GetItem;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetItemEndpoint
{
    internal static RouteGroupBuilder MapGetItem(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/items/{itemId}", static async (string itemId, IService<GetItemRequest, ItemView> service, CancellationToken ct) =>
        {
            var item = ItemRoute.Item(itemId);
            return item.IsError ? item.ToHttpResult() : (await service.Handle(new GetItemRequest(item.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
