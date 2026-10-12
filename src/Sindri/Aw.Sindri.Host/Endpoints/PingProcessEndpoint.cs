using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.PingProcess;

namespace Aw.Sindri.Host.Endpoints;

internal static class PingProcessEndpoint
{
    internal static RouteGroupBuilder MapPingProcess(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/ping", static async (IService<PingProcessRequest, Ping> service, CancellationToken ct)
            => (await service.Handle(new PingProcessRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
