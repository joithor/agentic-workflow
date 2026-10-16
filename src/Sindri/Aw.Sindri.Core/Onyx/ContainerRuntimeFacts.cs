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
public sealed record ContainerRuntimeFacts(bool CliFound, bool DaemonAnswers, string? ServerVersion, bool ComposeV2, string? ComposeVersion, long? MemoryBytes, string? DataRoot, string? Endpoint);
