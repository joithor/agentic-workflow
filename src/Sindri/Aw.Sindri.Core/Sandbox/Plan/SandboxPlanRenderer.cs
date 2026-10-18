namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The text of 'sindri sandbox show' and 'sindri sandbox explain': pure over the plan, so a session can ask with the state directory denied.</summary>
public static class SandboxPlanRenderer
{
    /// <summary>The whole plan as text.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The text.</returns>
    public static string Show(SandboxPlan plan) => throw new NotSupportedException();

    /// <summary>Why a path, a host or a command is allowed or denied, and how the owner can change it.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="query">A path, a host name or a command.</param>
    /// <returns>The answer.</returns>
    public static string Explain(SandboxPlan plan, string query) => throw new NotSupportedException();
}
