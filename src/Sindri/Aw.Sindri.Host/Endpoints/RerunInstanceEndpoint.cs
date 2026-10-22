using Aw.Kernel.Services;
using Aw.Sindri.Features.StartInstance;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// POST /sindri/workflow/instances/{instanceId}/rerun (desk): starts the same recipe again on the same item with the same inputs, as the next attempt, from an instance that ended
/// failed, unresolved or cancelled. The answer is the start's: the new instance, which carries <c language="text">again</c>.
/// </summary>
internal static class RerunInstanceEndpoint
{
    internal static RouteGroupBuilder MapRerunInstance(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow/instances/{instanceId}/rerun", static async (string instanceId, IService<RerunInstanceRequest, StartInstanceResponse> service, CancellationToken ct)
            => (await service.Handle(new RerunInstanceRequest(InstanceRoute.Id(instanceId)), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
