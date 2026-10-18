using Aw.Kernel;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The product default until SB-5a: no sandbox is built into this release, so every call is SND-LAUNCH-014. The launcher does not consult it for an attended open before SB-5b.</summary>
public sealed class UnavailableSandbox : ISandbox
{
    private static Error Refusal() =>
        SandboxRefusal.For(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.UnsupportedPlatform, "no sandbox is built into this release");

    /// <inheritdoc />
    public Result<SandboxCapabilities> Probe() => Refusal();

    /// <inheritdoc />
    public Result<SandboxPlan> Plan(SandboxRequest request) => Refusal();

    /// <inheritdoc />
    public Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct) => Task.FromResult<Result<Unit>>(Refusal());

    /// <inheritdoc />
    public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv) => Refusal();
}
