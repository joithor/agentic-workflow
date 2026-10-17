using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.CancelJob;

namespace Aw.Sindri.Host.Endpoints;

internal static class CancelJobEndpoint
{
    internal static RouteGroupBuilder MapCancelJob(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/jobs/{jobId}/cancel", static async (string jobId, IService<CancelJobRequest, CommandAccepted> service, CancellationToken ct) =>
        {
            var job = JobRoute.Job(jobId);
            return job.IsError ? job.ToHttpResult() : (await service.Handle(new CancelJobRequest(job.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
