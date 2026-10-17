using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.Launching;

/// <summary>Launches one request (UI spec E-1): the takeover, the claim, the launch under its limit, and the outcome.</summary>
internal sealed class ProcessLaunch : IService<ProcessLaunchRequest, ProcessLaunchResponse>
{
    public ValueTask<Result<ProcessLaunchResponse>> Handle(ProcessLaunchRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("U9 red: the launch is not written yet");
}
