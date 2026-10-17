using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.ResumeService;

namespace Aw.Sindri.Host.Endpoints;

internal static class ResumeServiceEndpoint
{
    internal static RouteGroupBuilder MapResumeService(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/services/{name}/resume", static async (string name, IService<ResumeServiceRequest, CommandAccepted> service, CancellationToken ct) =>
        {
            var named = JobRoute.Service(name);
            return named.IsError ? named.ToHttpResult() : (await service.Handle(new ResumeServiceRequest(named.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
