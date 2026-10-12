using Aw.Context.Core.Results;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/sources: each context source's state (context spec §8.5).</summary>
internal static class GetContextSourcesEndpoint
{
    internal static RouteGroupBuilder MapGetContextSources(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/sources", static async (IService<GetContextSourcesRequest, SourcesView> service, CancellationToken ct)
            => (await service.Handle(new GetContextSourcesRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
