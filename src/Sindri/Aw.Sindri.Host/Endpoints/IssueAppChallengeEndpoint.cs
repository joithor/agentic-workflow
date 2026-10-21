using Aw.Kernel.Services;
using Aw.Sindri.Features.IssueChallenge;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/workflow/challenges/app (setup-once spec §4 item 1; desk, the human token only): the body is the action text, one of the three the app confirms. A separate route, so the terminal route keeps its rule.</summary>
internal static class IssueAppChallengeEndpoint
{
    internal static RouteGroupBuilder MapIssueAppChallenge(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow/challenges/app", static async (IssueAppChallengeRequest request, IService<IssueAppChallengeRequest, IssueChallengeResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
