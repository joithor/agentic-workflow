using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.PauseService;

namespace Aw.Sindri.Host.Endpoints;

internal static class PauseServiceEndpoint
{
    internal static RouteGroupBuilder MapPauseService(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/services/{name}/pause", static async (string name, IService<PauseServiceRequest, CommandAccepted> service, CancellationToken ct) =>
        {
            var named = JobRoute.Service(name);
            return named.IsError ? named.ToHttpResult() : (await service.Handle(new PauseServiceRequest(named.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
