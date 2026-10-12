using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.RebuildViews;

namespace Aw.Sindri.Host.Endpoints;

internal static class RebuildViewsEndpoint
{
    internal static RouteGroupBuilder MapRebuildViews(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/views/rebuild", static async (IService<RebuildViewsRequest, CommandAccepted> service, CancellationToken ct)
            => (await service.Handle(new RebuildViewsRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
