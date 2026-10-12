namespace Aw.Sindri.Host.Endpoints;

internal static class StreamEventsEndpoint
{
    internal static RouteGroupBuilder MapStreamEvents(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/events", EventStream.Stream);
        return group;
    }
}
