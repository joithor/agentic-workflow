using Aw.Context.Core.Subgraph;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/context/subgraph?nodes=&amp;nodes=&amp;depth=&amp;mapVersion= (at most 20 keys) and POST /sindri/context/subgraph with the same fields as the JSON body (at most 1000):
/// one question, two transports, one service and one session filter. The POST is a query: the only body-query route (ARCH-CQRS-001; EndpointCqrsRules.BodyQueries). It takes no
/// idempotency key and writes no ledger event, honours X-Sindri-After, and refuses an Origin header as a command does.
/// </summary>
internal static class GetContextSubgraphEndpoint
{
    internal static RouteGroupBuilder MapGetContextSubgraph(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/subgraph", static async (string[]? nodes, int? depth, string? mapVersion, IService<GetContextSubgraphRequest, SubgraphView> subgraph, CancellationToken ct) =>
            (await subgraph.Handle(new GetContextSubgraphRequest(nodes ?? [], depth, mapVersion, FromUrl: true), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGroup(string.Empty).WithMetadata(CommandRouteMetadata.Instance).MapPost("/context/subgraph", static async (GetContextSubgraphRequest request, IService<GetContextSubgraphRequest, SubgraphView> subgraph, CancellationToken ct) =>
            (await subgraph.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
