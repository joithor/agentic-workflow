namespace Aw.Sindri.Core.Onyx;

/// <summary>What the box's container runtime reports (Docker today), as the Onyx preflight needs it.</summary>
/// <param name="CliFound">The docker CLI is on PATH.</param>
/// <param name="DaemonAnswers">The daemon answered `docker version` in time (false when it is down, still starting or hangs).</param>
/// <param name="ServerVersion">The daemon's version when it answers.</param>
/// <param name="ComposeV2">`docker compose version` works (the Compose v2 plugin; the legacy docker-compose v1 doesn't count).</param>
/// <param name="ComposeVersion">Compose's version when v2 is present.</param>
/// <param name="MemoryBytes">The memory Docker has (Docker Desktop's VM limit) when the daemon answers `docker info`.</param>
/// <param name="DataRoot">Docker's data root (`docker info` DockerRootDir) when the daemon answers: a host directory on a Linux engine, a path inside the VM on Docker Desktop.</param>
public sealed record ContainerRuntimeFacts(bool CliFound, bool DaemonAnswers, string? ServerVersion, bool ComposeV2, string? ComposeVersion, long? MemoryBytes, string? DataRoot);
