using Aw.Kernel.Services;
using Aw.Sindri.Features.ListCommands;

namespace Aw.Sindri.Host.Endpoints;

internal static class ListCommandsEndpoint
{
    internal static RouteGroupBuilder MapListCommands(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/commands", static async (IService<ListCommandsRequest, ListCommandsResponse> service, CancellationToken ct)
            => (await service.Handle(new ListCommandsRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
