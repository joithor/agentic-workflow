using Aw.Kernel.Services;
using Aw.Sindri.Features.ListItems;

namespace Aw.Sindri.Host.Endpoints;

internal static class ListItemsEndpoint
{
    internal static RouteGroupBuilder MapListItems(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/items", static async (string? state, string? repo, IService<ListItemsRequest, ListItemsResponse> service, CancellationToken ct) =>
        {
            var filter = ItemRoute.State(state).Bind(s => ItemRoute.Repo(repo).Map(r => new ListItemsRequest(s, r)));
            return filter.IsError ? filter.ToHttpResult() : (await service.Handle(filter.Value, ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
