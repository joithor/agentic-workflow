using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.DesktopRun;

/// <summary>Runs the desktop evidence (the red start: the behavior arrives with the next commit).</summary>
internal sealed class DesktopRunService : IService<DesktopRunRequest, DesktopRunResponse>
{
    public ValueTask<Result<DesktopRunResponse>> Handle(DesktopRunRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
