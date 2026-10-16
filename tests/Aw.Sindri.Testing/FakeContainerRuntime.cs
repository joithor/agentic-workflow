using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>The container runtime with scripted facts. Held to <see cref="ContainerRuntimeContract"/>.</summary>
/// <param name="facts">The facts it reports.</param>
public sealed class FakeContainerRuntime(ContainerRuntimeFacts facts) : IContainerRuntime
{
    /// <summary>A box with Docker 29.4.0 running with 16 GiB, and Compose v2.40.3.</summary>
    public static ContainerRuntimeFacts Healthy { get; } = new(
        CliFound: true, DaemonAnswers: true, ServerVersion: "29.4.0", ComposeV2: true, ComposeVersion: "v2.40.3", MemoryBytes: 16_818_978_816, DataRoot: "/var/lib/docker", Endpoint: "unix:///var/run/docker.sock");

    /// <summary>A box with no docker CLI.</summary>
    public static ContainerRuntimeFacts NoCli { get; } = new(
        CliFound: false, DaemonAnswers: false, ServerVersion: null, ComposeV2: false, ComposeVersion: null, MemoryBytes: null, DataRoot: null, Endpoint: null);

    /// <summary>A box with the CLI and Compose v2 whose daemon isn't answering (Docker Desktop not started).</summary>
    public static ContainerRuntimeFacts DaemonDown { get; } = Healthy with { DaemonAnswers = false, ServerVersion = null, MemoryBytes = null, DataRoot = null };

    /// <summary>A box with Docker running and only legacy docker-compose.</summary>
    public static ContainerRuntimeFacts ComposeV1Only { get; } = Healthy with { ComposeV2 = false, ComposeVersion = null };

    /// <summary>Gets or sets the facts the fake reports.</summary>
    public ContainerRuntimeFacts Facts { get; set; } = facts;

    /// <inheritdoc />
    public ContainerRuntimeFacts Inspect(CancellationToken cancellationToken) => Facts;
}
