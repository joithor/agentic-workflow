using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// Reads where the managed Onyx stands (read-only; the one reading `sindri onyx status` and the setup list share): the pins, the files `onyx up` keeps, Docker's containers
/// and the preflight's checks, and, while Onyx runs, whether it answers and with which release.
/// </summary>
/// <param name="pinsSource">The pins this build was made with.</param>
/// <param name="facts">Docker, the host and where Onyx's files live.</param>
/// <param name="health">Asks the running Onyx whether it answers.</param>
/// <param name="workspace">The files `onyx up` keeps.</param>
public sealed class OnyxStatusReader(IOnyxPinsSource pinsSource, OnyxFacts facts, IOnyxHealth health, IOnyxWorkspace workspace)
{
    /// <summary>Reads the status.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>The report, or the error when the compose file can't be read.</returns>
    public async ValueTask<Result<OnyxStatusReport>> Read(CancellationToken cancellationToken)
    {
        var pins = pinsSource.Pins;
        var state = workspace.ReadState();
        var (docker, host) = facts.Read(cancellationToken);
        var compose = workspace.Read(facts.Paths.Compose);
        if (compose.IsError)
        {
            return compose.Error;
        }

        var setUp = IsSetUp(state, compose.Value);
        var (running, servingPort, listFailed) = Serving(setUp, state, cancellationToken);
        var report = OnyxPreflight.Evaluate(docker, host, pins, running ? servingPort : null, facts.MemoryUsedByOthers(cancellationToken));
        var onyx = Classify(setUp, running, facts.DockerProblem(cancellationToken) is not null || listFailed);
        var (url, version) = running ? await Running(report.Port, cancellationToken).ConfigureAwait(false) : (null, null);
        return new OnyxStatusReport(onyx, pins.Version, pins.Commit, report.Checks, report.Port, report.Ok ? 0 : 1, url, version);
    }

    // Set up means `onyx up` finished its start step and the fetched files are still there.
    private static bool IsSetUp(OnyxSetupState? state, string? compose) => state is not null && state.Steps.ContainsKey(OnyxSteps.Start) && compose is not null;

    // Set up with a Docker that can't be asked: whether it runs is unknown, never a guess of stopped.
    private static OnyxState Classify(bool setUp, bool running, bool dockerUnusable)
        => !setUp ? OnyxState.NotSetUp : dockerUnusable ? OnyxState.Unknown : running ? OnyxState.Running : OnyxState.Stopped;

    // The address, and the release it reports (or unreachable when it doesn't answer).
    private async ValueTask<(string Url, string Version)> Running(int port, CancellationToken cancellationToken)
    {
        var probe = await health.Probe(port, cancellationToken).ConfigureAwait(false);
        return (string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}"), probe.Healthy ? probe.BackendVersion ?? "unreachable" : "unreachable");
    }

    // Whether Onyx is serving and the port it holds: the one nginx publishes (as in up and doctor), and state.json's only when no nginx publishes one; a state another home wrote is ignored.
    private (bool Running, int? Port, bool ListFailed) Serving(bool ask, OnyxSetupState? state, CancellationToken cancellationToken)
    {
        if (!ask)
        {
            return (false, null, false);
        }

        // With a file gone the stack is asked about by its label, as `down` does; a read that fails is "unknown", never "stopped".
        var picked = facts.ProjectInUse(workspace);
        var containers = picked.IsError ? picked.Error : facts.Containers(picked.Value.Project, cancellationToken);
        if (containers.IsError)
        {
            return (false, null, true);
        }

        var recorded = facts.ForeignProjectWarning(state) is null ? state?.Port : null;
        return (IsServing(containers.Value), OnyxFacts.OwnPort(containers.Value) ?? recorded, false);
    }

    // nginx and the API server both running is what "Onyx is up" means here; anything else is stopped. "Status" means "Onyx is serving" on purpose: down, doctor and
    // own-port use ComposeContainer.IsLive instead, because a restarting container is live (it holds its port and must be stopped) but not serving.
    private static bool IsServing(IReadOnlyList<ComposeContainer> containers)
        => containers.Any(static c => c.Service == "nginx" && c.State == "running")
            && containers.Any(static c => c.Service == "api_server" && c.State == "running");
}
