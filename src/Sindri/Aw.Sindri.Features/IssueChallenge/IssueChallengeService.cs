using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.IssueChallenge;

/// <summary>Red stub (engine spec Phase 2, unit (c)): the tests name the behavior; the service arrives next.</summary>
internal sealed class IssueChallengeService : IService<IssueChallengeRequest, IssueChallengeResponse>
{
    public ValueTask<Result<IssueChallengeResponse>> Handle(IssueChallengeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("IssueChallenge arrives with engine P2 unit (c)");
}
