using Aw.Kernel.Services;
using Aw.Sindri.Features.ListSessions;

namespace Aw.Sindri.Host.Endpoints;

internal static class ListSessionsEndpoint
{
    internal static RouteGroupBuilder MapListSessions(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/sessions", static async (string? provider, string? state, string? repo, string? inWorkflow, IService<ListSessionsRequest, SessionList> service, CancellationToken ct) =>
        {
            var filter = SessionRoute.Provider(provider).Bind(p => SessionRoute.State(state)
                .Bind(s => ItemRoute.Repo(repo).Bind(r => SessionRoute.InWorkflow(inWorkflow).Map(w => new ListSessionsRequest(p, s, r, w)))));
            return filter.IsError ? filter.ToHttpResult() : (await service.Handle(filter.Value, ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
