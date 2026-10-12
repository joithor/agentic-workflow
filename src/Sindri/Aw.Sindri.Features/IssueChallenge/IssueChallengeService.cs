using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.IssueChallenge;

/// <summary>P2 contract stub (engine spec Phase 2): issues a challenge for one protected action, with its preview.</summary>
internal sealed class IssueChallengeService : IService<IssueChallengeRequest, IssueChallengeResponse>
{
    public ValueTask<Result<IssueChallengeResponse>> Handle(IssueChallengeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("IssueChallenge arrives with engine P2 unit (c)");
}
