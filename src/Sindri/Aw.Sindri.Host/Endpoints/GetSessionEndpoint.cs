using Aw.Kernel.Services;
using Aw.Sindri.Features.GetSession;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetSessionEndpoint
{
    internal static RouteGroupBuilder MapGetSession(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/sessions/{provider}/{id}", static async (string provider, string id, IService<GetSessionRequest, SessionView> service, CancellationToken ct) =>
        {
            var session = SessionRoute.Ref(provider, id);
            return session.IsError ? session.ToHttpResult() : (await service.Handle(new GetSessionRequest(session.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
