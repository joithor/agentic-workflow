using Aw.Context.Core.Lenses;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetContextFileMapEndpoint
{
    internal static RouteGroupBuilder MapGetContextFileMap(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/filemap", static async (string? repo, string? path, bool? unmapped, bool? draft, string? cursor, int? limit, IService<GetContextFileMapRequest, FileMapView> service, CancellationToken ct)
            => (await service.Handle(new GetContextFileMapRequest(repo ?? string.Empty, path, unmapped ?? false, draft ?? false, cursor, limit), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
