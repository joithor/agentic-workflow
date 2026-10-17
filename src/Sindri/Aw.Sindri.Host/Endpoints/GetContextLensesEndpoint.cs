using Aw.Context.Core.Lenses;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/lenses?anchor=&amp;depth=&amp;draft=&amp;mapVersion=: one distinct anchor answers schema 1, a repeated 'anchor' of several answers schema 2.</summary>
internal static class GetContextLensesEndpoint
{
    internal static RouteGroupBuilder MapGetContextLenses(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/lenses", static async (string[]? anchor, int? depth, bool? draft, string? mapVersion, IService<GetContextLensesRequest, LensesView> single,
                IService<GetContextLensesSetRequest, LensesSetView> set, CancellationToken ct) =>
        {
            var anchors = (anchor ?? []).Distinct(StringComparer.Ordinal).ToArray();
            return anchors.Length <= 1
                ? (await single.Handle(new GetContextLensesRequest(anchors.Length == 0 ? string.Empty : anchors[0], draft ?? false, depth, mapVersion), ct).ConfigureAwait(false)).ToHttpResult()
                : (await set.Handle(new GetContextLensesSetRequest(anchors, draft ?? false, depth, mapVersion), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
