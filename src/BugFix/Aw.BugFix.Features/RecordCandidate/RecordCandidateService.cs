using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.RecordCandidate;

internal sealed class RecordCandidateService : IService<RecordCandidateRequest, RecordCandidateResponse>
{
    public ValueTask<Result<RecordCandidateResponse>> Handle(RecordCandidateRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("RecordCandidate: make RecordCandidateTests pass");
}
