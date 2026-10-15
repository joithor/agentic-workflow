using Aw.Kernel.Services;
using Aw.Sindri.Features.IssueChallenge;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/workflow/challenges (engine spec §4.2, §6.3; issue, human at a terminal only): the body is the action text.</summary>
internal static class IssueChallengeEndpoint
{
    internal static RouteGroupBuilder MapIssueChallenge(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow/challenges", static async (IssueChallengeRequest request, IService<IssueChallengeRequest, IssueChallengeResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
