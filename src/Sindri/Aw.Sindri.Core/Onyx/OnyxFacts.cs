using Aw.Kernel;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Core.Onyx;

/// <summary>Reads what the Onyx preflight judges (the container runtime, the disks and the loopback ports) and where Onyx's files live, from the ports that know.</summary>
/// <param name="runtime">The container runtime.</param>
/// <param name="probes">The host's disks and ports.</param>
/// <param name="home">The Sindri home.</param>
public sealed class OnyxFacts(IContainerRuntime runtime, IHostProbes probes, ISindriHome home)
{
    /// <summary>Gets the files' paths under the Sindri home.</summary>
    public OnyxPaths Paths { get; } = OnyxPaths.For(home.Path);

    /// <summary>Reads the facts: the runtime's, and the host's (free disk at the Sindri home, and at Docker's data root when that is a directory on this host, and the loopback ports).</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>The runtime's facts and the host's.</returns>
    public (ContainerRuntimeFacts Docker, HostFacts Host) Read(CancellationToken cancellationToken)
    {
        var docker = runtime.Inspect(cancellationToken);
        var host = new HostFacts(
            probes.FreeDisk(home.Path),
            docker.DataRoot is { } dataRoot ? probes.FreeDiskIfExists(dataRoot) : null,
            probes.Ports(OnyxPreflight.PreferredPort, OnyxPreflight.FallbackFirst, OnyxPreflight.FallbackLast));
        return (docker, host);
    }

    /// <summary>Reads the Docker server's platform.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>linux/arm64 or linux/amd64, or the error.</returns>
    public Result<string> Platform(CancellationToken cancellationToken) => runtime.ServerPlatform(cancellationToken);

    /// <summary>Gets the compose project to act on: the one `onyx up` recorded in state.json when it is a name Sindri could have made, else this home's own.</summary>
    /// <param name="state">The setup state, or null when there is none.</param>
    /// <returns>The project.</returns>
    public ComposeProject ProjectOf(OnyxSetupState? state)
        => ComposeProject.IsOurs(state?.Project) ? Paths.Project with { Name = state!.Project! } : Paths.Project;   // why: IsOurs is false for null

    /// <summary>Lists the containers of Sindri's Onyx project.</summary>
    /// <param name="state">The setup state (which project to ask about), or null.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>The containers, or the error (Docker isn't answering).</returns>
    public Result<IReadOnlyList<ComposeContainer>> Containers(OnyxSetupState? state, CancellationToken cancellationToken) => runtime.Ps(ProjectOf(state), cancellationToken);
}
