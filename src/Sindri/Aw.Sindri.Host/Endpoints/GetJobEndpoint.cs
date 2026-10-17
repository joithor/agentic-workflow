using Aw.Kernel.Services;
using Aw.Sindri.Features.GetJob;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetJobEndpoint
{
    internal static RouteGroupBuilder MapGetJob(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/jobs/{jobId}", static async (string jobId, IService<GetJobRequest, JobView> service, CancellationToken ct) =>
        {
            var job = JobRoute.Job(jobId);
            return job.IsError ? job.ToHttpResult() : (await service.Handle(new GetJobRequest(job.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
