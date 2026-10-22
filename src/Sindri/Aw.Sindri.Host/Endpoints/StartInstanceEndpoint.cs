using Aw.Kernel.Services;
using Aw.Sindri.Features.StartInstance;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/workflow/instances (engine spec rev 8 §6.3; desk): starts a recipe on an item, or returns the instance it already has.</summary>
internal static class StartInstanceEndpoint
{
    internal static RouteGroupBuilder MapStartInstance(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow/instances", static async (StartInstanceRequest request, IService<StartInstanceRequest, StartInstanceResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group.MapRerunInstance();   // the rerun is the same slice
    }
}
