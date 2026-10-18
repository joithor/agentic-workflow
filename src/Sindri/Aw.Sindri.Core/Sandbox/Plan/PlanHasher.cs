namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The plan hash: SHA-256 over a canonical text of the plan, first 12 hex digits. Per-launch paths are replaced by placeholders, so the same policy hashes the same across launches.</summary>
public static class PlanHasher
{
    /// <summary>Hashes a plan.</summary>
    /// <param name="plan">The plan (its own PlanHash is ignored).</param>
    /// <param name="perLaunchPaths">Placeholder and path pairs ('$WORKTREE', '/work/x'), replaced longest path first.</param>
    /// <returns>12 lower-case hex digits.</returns>
    public static string Hash(SandboxPlan plan, IReadOnlyList<(string Placeholder, string Path)> perLaunchPaths) => throw new NotSupportedException();
}
