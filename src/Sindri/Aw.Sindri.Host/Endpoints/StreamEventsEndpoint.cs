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

    private static Task<IResult> Stream(HttpContext context, IService<StreamEventsRequest, StreamEventsResponse> service, EventStreamOpener opener) =>
        opener.Open(context, service);
}
