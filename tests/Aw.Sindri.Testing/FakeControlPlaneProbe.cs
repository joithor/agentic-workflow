using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Testing;

/// <summary>The control-plane probe with a canned answer (unreachable until <see cref="Answer"/> is set).</summary>
public sealed class FakeControlPlaneProbe : IControlPlaneProbe
{
    /// <summary>The answer the probe gives.</summary>
    public Result<(ControlEndpoint Endpoint, Ping Ping)> Answer { get; set; } = SindriErrors.NotRunning.With("no control plane is recorded (fix: sindri)");

    /// <inheritdoc/>
    public ValueTask<Result<(ControlEndpoint Endpoint, Ping Ping)>> Check(CancellationToken cancellationToken) => ValueTask.FromResult(Answer);
}
