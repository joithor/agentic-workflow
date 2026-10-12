using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.StopProcess;

namespace Aw.Sindri.Host.Endpoints;

internal static class StopProcessEndpoint
{
    internal static RouteGroupBuilder MapStopProcess(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/stop", static async (IService<StopProcessRequest, StopProcessResponse> service, CancellationToken ct)
            => (await service.Handle(new StopProcessRequest(StopReason.Stop), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
