using Aw.Context.Core.Lenses;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetContextLensesEndpoint
{
    internal static RouteGroupBuilder MapGetContextLenses(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/lenses", static async (string? anchor, int? depth, bool? draft, IService<GetContextLensesRequest, LensesView> service, CancellationToken ct)
            => (await service.Handle(new GetContextLensesRequest(anchor ?? string.Empty, draft ?? false, depth), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
