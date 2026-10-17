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
    public (ContainerRuntimeFacts Docker, HostFacts Host) Read(CancellationToken cancellationToken) => Read(stickyPort: null, cancellationToken);

    /// <summary>Reads the facts, preferring the port a previous `onyx up` settled on: it stays while it is free (or ours) and the search moves on to the usual ports only when something else holds it.</summary>
    /// <param name="stickyPort">The port state.json recorded, or null for a first run (3000 first).</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>The runtime's facts and the host's.</returns>
    public (ContainerRuntimeFacts Docker, HostFacts Host) Read(int? stickyPort, CancellationToken cancellationToken)
    {
        var docker = runtime.Inspect(cancellationToken);
        var sticky = stickyPort is >= 1024 and <= 65535 and not OnyxPreflight.PreferredPort ? stickyPort : null;
        var ports = sticky is { } kept
            ? probes.Ports(kept, OnyxPreflight.PreferredPort, OnyxPreflight.FallbackLast)
            : probes.Ports(OnyxPreflight.PreferredPort, OnyxPreflight.FallbackFirst, OnyxPreflight.FallbackLast);
        var host = new HostFacts(
            probes.FreeDisk(home.Path),
            docker.DataRoot is { } dataRoot ? probes.FreeDiskIfExists(dataRoot) : null,
            ports);
        return (docker, host);
    }

    /// <summary>Reads the Docker server's platform.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>linux/arm64 or linux/amd64, or the error.</returns>
    public Result<string> Platform(CancellationToken cancellationToken) => runtime.ServerPlatform(cancellationToken);

    /// <summary>Gets the compose project to act on: always this home's own. A name recorded in state.json is never trusted to point elsewhere (see <see cref="ForeignProjectWarning"/>).</summary>
    public ComposeProject Project => Paths.Project;

    /// <summary>Tells when state.json names a compose project other than this home's own, which stop, ps and start refuse to act on.</summary>
    /// <param name="state">The setup state, or null when there is none.</param>
    /// <returns>The warning to show, or null when the state records no project or this home's own.</returns>
    public string? ForeignProjectWarning(OnyxSetupState? state)
        => state?.Project is { } recorded && !string.Equals(recorded, Paths.Project.Name, StringComparison.Ordinal) ? "state names another home's project; ignoring it" : null;

    /// <summary>Lists the containers of Sindri's Onyx project.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>The containers, or the error (Docker isn't answering).</returns>
    public Result<IReadOnlyList<ComposeContainer>> Containers(CancellationToken cancellationToken) => runtime.Ps(Project, cancellationToken);

    /// <summary>Finds the port our own Onyx holds: the one the project's live nginx publishes on 127.0.0.1, whatever state.json says (a start that failed or timed out leaves it up, and a lost state file doesn't make it someone else's). The one reading of it, for `up`, doctor and status.</summary>
    /// <param name="containers">The containers of Sindri's Onyx project.</param>
    /// <returns>The published port, or null when no live nginx publishes on loopback (stopped, or never started).</returns>
    public static int? OwnPort(IEnumerable<ComposeContainer> containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        return containers.Where(static c => c.Service == "nginx" && ComposeContainer.IsLive(c.State))
            .SelectMany(static c => c.Publishers)
            .FirstOrDefault(static p => p.Address == "127.0.0.1")?.PublishedPort;
    }
}
