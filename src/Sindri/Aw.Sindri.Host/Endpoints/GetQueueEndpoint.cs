using Aw.Kernel.Services;
using Aw.Sindri.Features.GetQueue;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetQueueEndpoint
{
    internal static RouteGroupBuilder MapGetQueue(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/queue", static async (IService<GetQueueRequest, QueueResponse> service, CancellationToken ct)
            => (await service.Handle(new GetQueueRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
