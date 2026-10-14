using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class ImportContextMapEndpoint
{
    internal static RouteGroupBuilder MapImportContextMap(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/context/map/import", static async (ImportContextMapRequest request, IService<ImportContextMapRequest, ImportContextMapResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
