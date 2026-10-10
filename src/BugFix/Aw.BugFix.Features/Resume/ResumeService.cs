using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.Resume;

internal sealed class ResumeService : IService<ResumeRequest, ResumeResponse>
{
    public ValueTask<Result<ResumeResponse>> Handle(ResumeRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("Resume: make ResumeTests pass");
}
