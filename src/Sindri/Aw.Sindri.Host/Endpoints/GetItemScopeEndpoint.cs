using Aw.Kernel.Services;
using Aw.Sindri.Features.GetItemScope;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetItemScopeEndpoint
{
    internal static RouteGroupBuilder MapGetItemScope(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/items/{itemId}/scope", static async (string itemId, IService<GetItemScopeRequest, ItemScopeResponse> service, CancellationToken ct) =>
        {
            var item = ItemRoute.Item(itemId);
            return item.IsError ? item.ToHttpResult() : (await service.Handle(new GetItemScopeRequest(item.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
