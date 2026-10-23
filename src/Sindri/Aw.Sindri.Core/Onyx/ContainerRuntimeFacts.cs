namespace Aw.Sindri.Core.Onyx;

/// <summary>What the box's container runtime reports (Docker today), as the Onyx preflight needs it.</summary>
/// <remarks>
/// A null <see cref="ServerVersion"/> or <see cref="ComposeVersion"/> beside a true <see cref="DaemonAnswers"/> or <see cref="ComposeV2"/> means
/// "not reported, or rejected as unsafe to print"; it does not say the version is old or new. P2 must add a distinct state for it before it compares versions.
/// </remarks>
/// <param name="CliFound">The docker CLI is on PATH.</param>
/// <param name="DaemonAnswers">The daemon answered `docker version` in time (false when it is down, still starting or hangs).</param>
/// <param name="ServerVersion">The daemon's version when it answers.</param>
/// <param name="ComposeV2">`docker compose version` works (the Compose v2 plugin; the legacy docker-compose v1 doesn't count).</param>
/// <param name="ComposeVersion">Compose's version when v2 is present.</param>
/// <param name="MemoryBytes">The memory Docker has (Docker Desktop's VM limit) when the daemon answers `docker info`.</param>
/// <param name="DataRoot">Docker's data root (`docker info` DockerRootDir) when the daemon answers: a host directory on a Linux engine, a path inside the VM on Docker Desktop.</param>
/// <param name="Endpoint">Where the CLI would send commands: DOCKER_HOST when set, else the current docker context's endpoint; null when the CLI is missing or the endpoint can't be read. Only a local one (unix:// or npipe://) is accepted.</param>
/// <param name="OperatingSystem">The engine's operating system (`docker info` OperatingSystem) when the daemon answers: "Docker Desktop" or "OrbStack" for a VM runtime, the distribution's name for a Linux engine.</param>
/// <param name="DaemonProblem">Why the daemon didn't answer, from the CLI's stderr, when <paramref name="DaemonAnswers"/> is false and the CLI is found; <see cref="DaemonProblem.None"/> when it answers.</param>
/// <param name="WaitedSeconds">How long a daemon that doesn't answer was given in all: the CLI's own budget for one that timed out (<see cref="DaemonProblem.Starting"/>, 25 s), or the whole wait when `onyx up` waited for it (<see cref="OnyxFacts.NoteWait"/>); null otherwise.</param>
public sealed record ContainerRuntimeFacts(
    bool CliFound, bool DaemonAnswers, string? ServerVersion, bool ComposeV2, string? ComposeVersion, long? MemoryBytes, string? DataRoot, string? Endpoint, string? OperatingSystem = null,
    DaemonProblem DaemonProblem = DaemonProblem.None, int? WaitedSeconds = null)
{
    /// <summary>Gets a value indicating whether Docker's data lives in a VM (Docker Desktop, OrbStack), so the data root is not a directory on this host even if a directory of that name exists here.</summary>
    public bool DataInVm => OperatingSystem is { } os
        && (os.Contains("Desktop", StringComparison.OrdinalIgnoreCase) || os.Contains("OrbStack", StringComparison.OrdinalIgnoreCase));

    /// <summary>Gets which runtime serves the CLI: from <see cref="OperatingSystem"/> when the daemon answered, else from the <see cref="Endpoint"/>'s socket path.</summary>
    public ContainerRuntimeKind Kind => OperatingSystem is { } os ? ByOperatingSystem(os) : BySocket(Endpoint);

    /// <summary>Gets a value indicating whether the daemon may still come up by itself: a local one (or one whose endpoint couldn't be read) that is starting or not running, as opposed to a missing CLI, a refused socket, a missing context or another machine.</summary>
    public bool DaemonMayStillStart => CliFound && !DaemonAnswers && DaemonProblem is DaemonProblem.Starting or DaemonProblem.NotRunning
        && (Endpoint is null || OnyxPreflight.IsLocalEndpoint(Endpoint));

    // `docker info` OperatingSystem names the app when the daemon answers.
    private static ContainerRuntimeKind ByOperatingSystem(string os)
        => os.Contains("OrbStack", StringComparison.OrdinalIgnoreCase) ? ContainerRuntimeKind.OrbStack
            : os.Contains("Docker Desktop", StringComparison.OrdinalIgnoreCase) ? ContainerRuntimeKind.DockerDesktop
            : ContainerRuntimeKind.Docker;

    // When it doesn't, the socket path says which app made it: OrbStack's ~/.orbstack/run/docker.sock; Docker Desktop's ~/.docker/run/docker.sock,
    // ~/.docker/desktop/docker.sock on Linux, or its Windows pipe. The text is only matched, never shown. /var/run/docker.sock names nobody: either
    // app may link it there, and so may a Linux engine.
    private static ContainerRuntimeKind BySocket(string? endpoint)
        => endpoint is null ? ContainerRuntimeKind.Docker
            : endpoint.Contains("/.orbstack/", StringComparison.Ordinal) ? ContainerRuntimeKind.OrbStack
            : endpoint.Contains("/.docker/run/docker.sock", StringComparison.Ordinal) || endpoint.Contains("/.docker/desktop/", StringComparison.Ordinal)
                || endpoint.Contains("dockerDesktop", StringComparison.OrdinalIgnoreCase) ? ContainerRuntimeKind.DockerDesktop
            : ContainerRuntimeKind.Docker;
}
