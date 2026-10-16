using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxStatus;

internal sealed class OnyxStatusService(IOnyxPinsSource pinsSource, OnyxFacts facts, IOnyxHealth health, IOnyxWorkspace workspace)
    : IService<OnyxStatusRequest, OnyxStatusResponse>
{
    public async ValueTask<Result<OnyxStatusResponse>> Handle(OnyxStatusRequest request, CancellationToken cancellationToken)
    {
        var pins = pinsSource.Pins;
        var state = workspace.ReadState();
        var (docker, host) = facts.Read(cancellationToken);
        var setUp = IsSetUp(state);
        var running = setUp && docker.DaemonAnswers && IsRunning(cancellationToken);
        var report = OnyxPreflight.Evaluate(docker, host, pins, running ? state?.Port : null);
        var onyx = Classify(setUp, running);
        var (url, version) = running ? await Running(report.Port, cancellationToken).ConfigureAwait(false) : (null, null);
        return new OnyxStatusResponse(onyx, pins.Version, pins.Commit, report.Checks, report.Port, report.Ok ? 0 : 1, url, version, Detail(onyx));
    }

    // Set up means `onyx up` finished its start step and the fetched files are still there.
    private bool IsSetUp(OnyxSetupState? state) => state is not null && state.Steps.ContainsKey(OnyxSteps.Start) && workspace.Read(facts.Paths.Compose) is not null;

    private static OnyxState Classify(bool setUp, bool running) => !setUp ? OnyxState.NotSetUp : running ? OnyxState.Running : OnyxState.Stopped;

    // The address, and the release it reports (or unreachable when it doesn't answer).
    private async ValueTask<(string Url, string Version)> Running(int port, CancellationToken cancellationToken)
    {
        var probe = await health.Probe(port, cancellationToken).ConfigureAwait(false);
        return (string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}"), probe.Healthy ? probe.BackendVersion ?? "unreachable" : "unreachable");
    }

    private static string Detail(OnyxState state) => state switch
    {
        OnyxState.NotSetUp => "not set up: run `sindri onyx up`",
        OnyxState.Stopped => "stopped: run `sindri onyx up` to start it",
        OnyxState.Running => "running",
        _ => "",
    };

    // nginx and the API server both running is what "Onyx is up" means here; anything else is stopped.
    private bool IsRunning(CancellationToken cancellationToken)
    {
        var containers = facts.Containers(cancellationToken);
        return containers.IsSuccess
            && containers.Value.Any(static c => c.Service == "nginx" && c.State == "running")
            && containers.Value.Any(static c => c.Service == "api_server" && c.State == "running");
    }
}
