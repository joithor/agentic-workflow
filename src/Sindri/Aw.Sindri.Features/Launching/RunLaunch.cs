using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.Launching;

/// <summary>One tick of the launcher service (UI spec E-1).</summary>
internal sealed class RunLaunch : IService<RunLaunchRequest, RunLaunchResponse>
{
    public ValueTask<Result<RunLaunchResponse>> Handle(RunLaunchRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("U9 red: the launcher tick is not written yet");
}
