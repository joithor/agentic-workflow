using Aw.Kernel.Services;
using Aw.Sindri.Features.ListJobs;

namespace Aw.Sindri.Host.Endpoints;

internal static class ListJobsEndpoint
{
    internal static RouteGroupBuilder MapListJobs(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/jobs", static async (string? state, string? kind, IService<ListJobsRequest, JobList> service, CancellationToken ct) =>
        {
            var parsedState = JobRoute.State(state);
            if (parsedState.IsError)
            {
                return parsedState.ToHttpResult();
            }

            var parsedKind = JobRoute.Kind(kind);
            return parsedKind.IsError
                ? parsedKind.ToHttpResult()
                : (await service.Handle(new ListJobsRequest(parsedState.Value, parsedKind.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
