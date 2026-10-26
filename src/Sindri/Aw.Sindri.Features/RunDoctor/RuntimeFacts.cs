using Aw.Kernel;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>Reads what the doctor's api, Temporal and build checks judge: the control plane's answer, the Temporal server and the build stamps.</summary>
/// <param name="api">Pings the running control plane.</param>
/// <param name="temporal">Inspects the Temporal server.</param>
/// <param name="builds">The running and installed build stamps; none outside an install.</param>
internal sealed class RuntimeFacts(IControlPlaneProbe api, ITemporalServer temporal, IBuildStamps? builds = null)
{
    public ValueTask<Result<(ControlEndpoint Endpoint, Ping Ping)>> Api(CancellationToken cancellationToken) => api.Check(cancellationToken);

    public ValueTask<TemporalFacts> Temporal(CancellationToken cancellationToken) => temporal.Inspect(cancellationToken);

    /// <summary>The stale build check (#1032): doctor warns when the stamps differ.</summary>
    public void CheckBuild()
    {
        if (builds is not null)
        {
            throw new NotSupportedException("the stale build check (#1032) is not built yet");   // red first
        }
    }
}
