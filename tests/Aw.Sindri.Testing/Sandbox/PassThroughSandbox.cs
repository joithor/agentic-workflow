using Aw.Kernel;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Testing.Sandbox;

/// <summary>
/// The test-only sandbox (sandbox spec SB12): Wrap returns the argv unchanged and SelfCheck passes. Only test hosts register it,
/// never the published product Host and never through a switch.
/// </summary>
public sealed class PassThroughSandbox : ISandbox
{
    /// <inheritdoc />
    public Result<SandboxCapabilities> Probe() => throw new NotSupportedException();

    /// <inheritdoc />
    public Result<SandboxPlan> Plan(SandboxRequest request) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct) => throw new NotSupportedException();

    /// <inheritdoc />
    public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv) => throw new NotSupportedException();
}
