using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.CloseItem;

namespace Aw.Sindri.Host.Endpoints;

internal static class CloseItemEndpoint
{
    internal static RouteGroupBuilder MapCloseItem(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/items/{itemId}/close", static async (string itemId, CloseItemBody body, IService<CloseItemRequest, CommandAccepted> service, CancellationToken ct) =>
        {
            var item = ItemRoute.Item(itemId);
            return item.IsError ? item.ToHttpResult() : (await service.Handle(body.ToRequest(item.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
