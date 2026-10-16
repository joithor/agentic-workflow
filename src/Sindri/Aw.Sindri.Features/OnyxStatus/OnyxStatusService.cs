using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxStatus;

internal sealed class OnyxStatusService(IOnyxPinsSource pinsSource, IContainerRuntime runtime, IHostProbes probes, ISindriHome home)
    : IService<OnyxStatusRequest, OnyxStatusResponse>
{
    public ValueTask<Result<OnyxStatusResponse>> Handle(OnyxStatusRequest request, CancellationToken cancellationToken)
    {
        var pins = pinsSource.Pins;
        var report = OnyxPreflight.Evaluate(
            runtime.Inspect(cancellationToken),
            probes.FreeDisk(home.Path),
            probes.Ports(OnyxPreflight.PreferredPort, OnyxPreflight.FallbackFirst, OnyxPreflight.FallbackLast),
            pins);
        Result<OnyxStatusResponse> response = new OnyxStatusResponse("not-set-up", pins.Version, pins.Commit, report.Checks, report.Port, report.Ok ? 0 : 1);
        return ValueTask.FromResult(response);
    }
}
