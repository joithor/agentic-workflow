using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.JudgeCandidate;

internal sealed class JudgeCandidateService : IService<JudgeCandidateRequest, JudgeCandidateResponse>
{
    public ValueTask<Result<JudgeCandidateResponse>> Handle(JudgeCandidateRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("JudgeCandidate: make JudgeCandidateTests pass");
}
