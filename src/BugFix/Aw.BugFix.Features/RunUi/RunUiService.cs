using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.RunUi;

internal sealed class RunUiService : IService<RunUiRequest, RunUiResponse>
{
    public ValueTask<Result<RunUiResponse>> Handle(RunUiRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("RunUi: make RunUiTests pass");
}
