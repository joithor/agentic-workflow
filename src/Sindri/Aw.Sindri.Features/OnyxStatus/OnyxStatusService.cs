using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxStatus;

internal sealed class OnyxStatusService(IOnyxPinsSource pinsSource, OnyxFacts facts, IOnyxHealth health, IOnyxWorkspace workspace)
    : IService<OnyxStatusRequest, OnyxStatusResponse>
{
    private readonly OnyxStatusReader _reader = new(pinsSource, facts, health, workspace);

    public async ValueTask<Result<OnyxStatusResponse>> Handle(OnyxStatusRequest request, CancellationToken cancellationToken)
    {
        var read = await _reader.Read(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var report = read.Value;
        return new OnyxStatusResponse(report.State, report.PinnedVersion, report.PinnedCommit, report.Checks, report.Port, report.ExitCode, report.Url, report.RunningVersion, Detail(report.State));
    }

    private static string Detail(OnyxState state) => state switch
    {
        OnyxState.NotSetUp => "not set up: run `sindri onyx up`",
        OnyxState.Stopped => "stopped: run `sindri onyx up` to start it",
        OnyxState.Running => "running",
        OnyxState.Unknown => "unknown: Docker isn't usable here, or its containers couldn't be listed, so whether Onyx runs can't be told",
        _ => "",
    };
}
