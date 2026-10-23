namespace Aw.Sindri.Core.Onyx;

/// <summary>Which container runtime serves the docker CLI here, as far as Sindri can tell: what a message names and which fix it gives.</summary>
public enum ContainerRuntimeKind
{
    /// <summary>A runtime Sindri doesn't recognize (a Linux engine, Colima, or a socket path that says nothing): the message says Docker.</summary>
    Docker,

    /// <summary>Docker Desktop.</summary>
    DockerDesktop,

    /// <summary>OrbStack.</summary>
    OrbStack,
}
