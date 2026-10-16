using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Core.Onyx;

/// <summary>The decision of whether this box can run the pinned Onyx (spec §4.1 step 1, §6). Pure: the facts come in, the report goes out.</summary>
public static class OnyxPreflight
{
    /// <summary>The port Onyx's web server wants on 127.0.0.1.</summary>
    public const int PreferredPort = 3000;

    /// <summary>The first fallback port.</summary>
    public const int FallbackFirst = 3001;

    /// <summary>The last fallback port, inclusive.</summary>
    public const int FallbackLast = 3010;

    /// <summary>Judges the facts against the pins.</summary>
    /// <param name="docker">What the container runtime reports.</param>
    /// <param name="disk">Free disk where Onyx would live.</param>
    /// <param name="port">Which loopback ports are free.</param>
    /// <param name="pins">What the pinned release needs.</param>
    /// <returns>The five checks and the port setup would use.</returns>
    public static PreflightReport Evaluate(ContainerRuntimeFacts docker, DiskFacts disk, PortFacts port, OnyxPins pins)
    {
        ArgumentNullException.ThrowIfNull(docker);
        ArgumentNullException.ThrowIfNull(disk);
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(pins);
        var dockerCheck = Docker(docker);
        var needsDocker = dockerCheck.State != CheckState.Ok;
        PreflightCheck[] checks =
        [
            dockerCheck,
            needsDocker ? Warn("compose", "needs docker") : Compose(docker),
            needsDocker ? Warn("memory", "needs docker") : Memory(docker, pins),
            Disk(disk, pins),
            Port(port),
        ];
        return new PreflightReport(checks, port.PreferredFree ? port.Preferred : port.FirstFreeFallback ?? port.Preferred);
    }

    /// <summary>Formats bytes as GiB with one decimal, the way the preflight prints them.</summary>
    /// <param name="bytes">A byte count.</param>
    /// <returns>For example 11.0.</returns>
    public static string Gib(long bytes) => (bytes / (double)(1L << 30)).ToString("F1", CultureInfo.InvariantCulture);

    private static PreflightCheck Docker(ContainerRuntimeFacts docker) => docker switch
    {
        { CliFound: false } => Fail("docker", "docker CLI not found", SindriErrors.OnyxDockerUnavailable, "Docker isn't installed: install Docker Desktop (or Docker Engine), then run this again"),
        { DaemonAnswers: false } => Fail("docker", "docker CLI found, daemon not answering", SindriErrors.OnyxDockerUnavailable, "Docker isn't running: start Docker Desktop (or the docker service) and wait until it is ready, then run this again"),
        _ => Ok("docker", docker.ServerVersion ?? "running"),
    };

    private static PreflightCheck Compose(ContainerRuntimeFacts docker) => docker.ComposeV2
        ? Ok("compose", docker.ComposeVersion ?? "v2")
        : Fail("compose", "Compose v2 not found", SindriErrors.OnyxComposeMissing, "Docker Compose v2 is missing (`docker compose version` fails; the legacy docker-compose isn't enough): update Docker Desktop or install the docker compose plugin");

    private static PreflightCheck Memory(ContainerRuntimeFacts docker, OnyxPins pins)
    {
        var need = Gib(pins.DockerMemoryBytes);
        if (docker.MemoryBytes is not { } have)
        {
            return Warn("memory", $"Docker's memory is unknown, needs {need} GiB");
        }

        var detail = $"{Gib(have)} GiB for Docker, needs {need} GiB";
        return have >= pins.DockerMemoryBytes
            ? Ok("memory", detail)
            : Fail("memory", detail, SindriErrors.OnyxNotEnoughResources, $"Docker has {Gib(have)} GiB and Onyx needs {need} GiB: raise it in Docker Desktop, Settings, Resources, Memory, to at least {need} GiB");
    }

    private static PreflightCheck Disk(DiskFacts disk, OnyxPins pins)
    {
        var detail = $"{Gib(disk.FreeBytes)} GiB free at {disk.MeasuredPath}, needs {Gib(pins.DiskBytes)} GiB";
        return disk.FreeBytes >= pins.DiskBytes
            ? Ok("disk", detail)
            : Fail("disk", detail, SindriErrors.OnyxNotEnoughResources, $"Not enough disk: {detail}. Free space on that volume, then run this again");
    }

    private static PreflightCheck Port(PortFacts port)
    {
        var preferred = port.Preferred.ToString(CultureInfo.InvariantCulture);
        if (port.PreferredFree)
        {
            return Ok("port", $"{preferred} free");
        }

        return port.FirstFreeFallback is { } fallback
            ? Ok("port", $"{preferred} taken; would use {fallback.ToString(CultureInfo.InvariantCulture)}")
            : Fail(
                "port",
                $"{preferred} taken, no free port in {FallbackFirst.ToString(CultureInfo.InvariantCulture)}-{FallbackLast.ToString(CultureInfo.InvariantCulture)}",
                SindriErrors.OnyxPortTaken,
                $"Port {preferred} is taken by something else and so is every port from {FallbackFirst.ToString(CultureInfo.InvariantCulture)} to {FallbackLast.ToString(CultureInfo.InvariantCulture)}: stop what uses them, then run this again");
    }

    private static PreflightCheck Ok(string name, string detail) => new(name, CheckState.Ok, detail, Problem: null);

    private static PreflightCheck Warn(string name, string detail) => new(name, CheckState.Warn, detail, Problem: null);

    private static PreflightCheck Fail(string name, string detail, ErrorDefinition code, string message)
        => new(name, CheckState.Fail, detail, code.With(message));
}
