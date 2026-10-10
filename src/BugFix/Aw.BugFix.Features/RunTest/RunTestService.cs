using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.RunTest;

internal sealed class RunTestService : IService<RunTestRequest, RunTestResponse>
{
    public ValueTask<Result<RunTestResponse>> Handle(RunTestRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("RunTest: make RunTestTests pass");
}
