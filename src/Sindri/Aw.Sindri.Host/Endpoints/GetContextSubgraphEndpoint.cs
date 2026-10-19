using Aw.Context.Core.Subgraph;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/context/subgraph?nodes=&amp;nodes=&amp;depth=&amp;mapVersion= (at most 20 keys) and QUERY /sindri/context/subgraph with the same fields as the JSON body (at most 1000):
/// one question, two transports, one service and one session filter. QUERY is the HTTP method for a safe read whose question is too large for a URL
/// (draft-ietf-httpbis-safe-method-w-body); a POST to the route is a 405. It is a query like the GET (ARCH-CQRS-001: every QUERY serves an IQuery): it takes no
/// idempotency key and writes no ledger event, honours X-Sindri-After, and refuses an Origin header as a command does.
/// </summary>
internal static class GetContextSubgraphEndpoint
{
    internal static RouteGroupBuilder MapGetContextSubgraph(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/subgraph", static async (string[]? nodes, int? depth, string? mapVersion, IService<GetContextSubgraphRequest, SubgraphView> subgraph, CancellationToken ct) =>
            (await subgraph.Handle(new GetContextSubgraphRequest(nodes ?? [], depth, mapVersion, FromUrl: true), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGroup(string.Empty).WithMetadata(BodyQueryRouteMetadata.Instance).MapMethods("/context/subgraph", [HttpMethods.Query], static async (GetContextSubgraphRequest request, IService<GetContextSubgraphRequest, SubgraphView> subgraph, CancellationToken ct) =>
            (await subgraph.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
