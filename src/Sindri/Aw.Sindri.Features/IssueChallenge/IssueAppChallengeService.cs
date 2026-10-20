using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.IssueChallenge;

/// <summary>Issues the app's challenge for one of the three actions it may confirm (setup-once spec §4 item 1).</summary>
/// <param name="issuer">The terminal route's issuing, which plans each action and builds its preview.</param>
internal sealed class IssueAppChallengeService(IssueChallengeService issuer) : IService<IssueAppChallengeRequest, IssueChallengeResponse>
{
    public ValueTask<Result<IssueChallengeResponse>> Handle(IssueAppChallengeRequest request, CancellationToken cancellationToken)
    {
        _ = issuer;
        throw new NotSupportedException();
    }
}
