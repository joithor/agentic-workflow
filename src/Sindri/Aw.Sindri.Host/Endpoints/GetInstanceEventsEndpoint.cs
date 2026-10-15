using Aw.Kernel.Services;
using Aw.Sindri.Features.GetInstanceEvents;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/workflow/instances/{instanceId}/events[?after=] (engine spec §6.3; query): the instance's events from the
/// ledger, oldest first, after the ledger sequence 'after' (default 0), with the cursor to read after next. The route takes
/// the instance's opaque id ('WFI-…'); an address or a bare item is refused (SND-CLI-003).
/// </summary>
internal static class GetInstanceEventsEndpoint
{
    internal static RouteGroupBuilder MapGetInstanceEvents(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/instances/{instanceId}/events", static async (string instanceId, long? after, IService<GetInstanceEventsRequest, GetInstanceEventsResponse> service, CancellationToken ct)
            => (await service.Handle(new GetInstanceEventsRequest(InstanceRoute.Id(instanceId), after ?? 0), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
