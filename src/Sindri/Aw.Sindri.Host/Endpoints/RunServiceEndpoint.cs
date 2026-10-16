using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.RunService;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/services/{name}/run: the generic job route, with the request built from the '{name}' route value instead of a body.</summary>
internal static class RunServiceEndpoint
{
    internal static RouteGroupBuilder MapRunService(this RouteGroupBuilder group) =>
        group.MapJob<RunServiceRequest, ScheduledResult>("/services/{name}/run", static http =>
            JobRoute.Service(http.Request.RouteValues["name"]?.ToString() ?? string.Empty).Map(static name => new RunServiceRequest(name)));
}
