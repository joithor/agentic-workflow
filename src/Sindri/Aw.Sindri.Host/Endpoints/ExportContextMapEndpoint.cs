using Aw.Context.Core.Map;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class ExportContextMapEndpoint
{
    internal static RouteGroupBuilder MapExportContextMap(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/map/export", static async (string? path, IService<ExportContextMapRequest, MapExported> service, CancellationToken ct)
            => (await service.Handle(new ExportContextMapRequest(path), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
