using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/context/sources/sync and POST /sindri/context/map/build: the context engine's two jobs on the generic job route.</summary>
internal static class ContextJobEndpoints
{
    internal static RouteGroupBuilder MapContextJobs(this RouteGroupBuilder group) =>
        group.MapJob<SyncContextSourcesRequest, SyncContextSourcesResult>("/context/sources/sync")
            .MapJob<BuildContextMapRequest, BuildContextMapResult>("/context/map/build", static _ => new BuildContextMapRequest());   // the build takes no input: any body, or none, starts it
}
