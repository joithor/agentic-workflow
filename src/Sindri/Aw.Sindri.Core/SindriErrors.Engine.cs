using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-ENGINE-001: the Temporal server, or the SDK call to it, didn't answer (API spec §3.8, E-30).</summary>
    public static readonly ErrorDefinition EngineUnavailable = new("SND-ENGINE-001", ErrorKind.Unavailable, "The Temporal server didn't answer (fix: sindri doctor; sindri serve starts the dev server)");

    /// <summary>SND-ENGINE-404: Temporal has no execution for that address, a retention edge (API spec §3.8, E-30).</summary>
    public static readonly ErrorDefinition EngineNotFound = new("SND-ENGINE-404", ErrorKind.NotFound, "Temporal has no execution for that address");
}
