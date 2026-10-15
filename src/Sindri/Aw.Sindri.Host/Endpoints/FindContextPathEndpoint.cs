using Aw.Context.Core.Graph;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/path?from=&amp;to= (context spec §8.5).</summary>
internal static class FindContextPathEndpoint
{
    internal static RouteGroupBuilder MapFindContextPath(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/path", static async (IService<FindContextPathRequest, PathView> service, string? from, string? to, CancellationToken ct)
            => (await service.Handle(new FindContextPathRequest(from ?? string.Empty, to ?? string.Empty), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
