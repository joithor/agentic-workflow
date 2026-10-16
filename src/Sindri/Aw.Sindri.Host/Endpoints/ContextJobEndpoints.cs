using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/context/sources/sync, /map/build and /eval/backtest: the context engine's three jobs on the generic job route.</summary>
internal static class ContextJobEndpoints
{
    internal static RouteGroupBuilder MapContextJobs(this RouteGroupBuilder group) =>
        group.MapJob<SyncContextSourcesRequest, SyncContextSourcesResult>("/context/sources/sync")
            .MapJob<BuildContextMapRequest, BuildContextMapResult>("/context/map/build", static _ => new BuildContextMapRequest())   // the build takes no input: any body, or none, starts it
            .MapJob<RunContextBacktestRequest, RunContextBacktestResult>("/context/eval/backtest");
}
