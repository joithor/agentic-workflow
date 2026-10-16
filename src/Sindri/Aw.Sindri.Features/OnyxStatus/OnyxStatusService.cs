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
        var docker = runtime.Inspect(cancellationToken);
        var report = OnyxPreflight.Evaluate(
            docker,
            probes.FreeDisk(home.Path),
            DataRootOnThisHost(docker.DataRoot),
            probes.Ports(OnyxPreflight.PreferredPort, OnyxPreflight.FallbackFirst, OnyxPreflight.FallbackLast),
            pins);
        Result<OnyxStatusResponse> response = new OnyxStatusResponse(OnyxState.NotSetUp, pins.Version, pins.Commit, report.Checks, report.Port, report.Ok ? 0 : 1);
        return ValueTask.FromResult(response);
    }

    // FreeDisk walks up to the nearest existing ancestor, so the data root is a directory on this host only when it is what got measured.
    // A Linux engine's /var/lib/docker is; Docker Desktop's, inside its VM, isn't there and walks up to something unrelated.
    private DiskFacts? DataRootOnThisHost(string? dataRoot)
    {
        if (dataRoot is null)
        {
            return null;
        }

        var measured = probes.FreeDisk(dataRoot);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(measured.MeasuredPath)) == Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot)) ? measured : null;
    }
}
