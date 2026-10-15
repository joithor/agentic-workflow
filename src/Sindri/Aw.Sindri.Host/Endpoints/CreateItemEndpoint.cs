using Aw.Kernel.Services;
using Aw.Sindri.Features.CreateItem;

namespace Aw.Sindri.Host.Endpoints;

internal static class CreateItemEndpoint
{
    internal static RouteGroupBuilder MapCreateItem(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/items", static async (CreateItemRequest request, IService<CreateItemRequest, ItemCreatedResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
