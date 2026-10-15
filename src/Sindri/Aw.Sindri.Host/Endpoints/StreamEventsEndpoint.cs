using Aw.Kernel.Services;
using Aw.Sindri.Features.StreamEvents;

namespace Aw.Sindri.Host.Endpoints;

internal static class StreamEventsEndpoint
{
    internal static RouteGroupBuilder MapStreamEvents(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/events", Stream);
        return group;
    }

    // The opener and its loop are built per request from the container's services, so they need no registration of their own.
    private static Task<IResult> Stream(HttpContext context, IService<StreamEventsRequest, StreamEventsResponse> service) =>
        ActivatorUtilities.CreateInstance<EventStreamOpener>(context.RequestServices)
            .Open(context, service, ActivatorUtilities.CreateInstance<EventPump>(context.RequestServices));
}
