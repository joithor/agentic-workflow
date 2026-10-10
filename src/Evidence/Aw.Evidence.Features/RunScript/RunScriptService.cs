using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.RunScript;

internal sealed class RunScriptService : IService<RunScriptRequest, RunScriptResponse>
{
    public ValueTask<Result<RunScriptResponse>> Handle(RunScriptRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("RunScript: make RunScriptTests pass");
}
