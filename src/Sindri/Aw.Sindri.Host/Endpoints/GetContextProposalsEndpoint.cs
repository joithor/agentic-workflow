using Aw.Context.Core.Proposals;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetContextProposalsEndpoint
{
    internal static RouteGroupBuilder MapGetContextProposals(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/map/proposals", static async (string? run, IService<GetContextProposalsRequest, ProposalsView> service, CancellationToken ct)
            => (await service.Handle(new GetContextProposalsRequest(run ?? string.Empty), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
