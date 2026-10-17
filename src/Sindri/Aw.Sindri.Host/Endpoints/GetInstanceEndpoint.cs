using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstance;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/workflow/instances/{instanceId} (engine spec §6.3; query): the instance as its 'GetInstance' Query shows it.
/// The route takes the instance's opaque id ('WFI-…'); an address or a bare item is refused (SND-CLI-003).
/// </summary>
internal static class GetInstanceEndpoint
{
    internal static RouteGroupBuilder MapGetInstance(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/instances/{instanceId}", static async (string instanceId, IService<GetInstanceRequest, InstanceDetail> service, CancellationToken ct)
            => (await service.Handle(new GetInstanceRequest(InstanceRoute.Id(instanceId)), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
