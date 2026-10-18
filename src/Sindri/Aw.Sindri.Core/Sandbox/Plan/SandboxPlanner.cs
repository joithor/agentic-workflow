using Aw.Kernel;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The pure plan builder (sandbox spec SB12): every adapter's Plan calls it and adds only its mechanism.</summary>
public static class SandboxPlanner
{
    /// <summary>Builds the plan for one launch.</summary>
    /// <param name="mechanism">The mechanism of the calling adapter.</param>
    /// <param name="request">The launch.</param>
    /// <returns>The plan, or SND-LAUNCH-015 (can't be confined to this policy), -016 (the plan itself is unsafe) or -017 (the login is not from the chosen source).</returns>
    public static Result<SandboxPlan> Build(SandboxMechanism mechanism, SandboxRequest request) => throw new NotSupportedException();

    /// <summary>A resume or reopen reuses the stored plan; a missing plan is refused (SB1).</summary>
    /// <param name="stored">The stored plan, or null when it is gone.</param>
    /// <returns>The plan, or SND-LAUNCH-015 'no-stored-plan'.</returns>
    public static Result<SandboxPlan> FromStored(SandboxPlan? stored) => throw new NotSupportedException();
}
