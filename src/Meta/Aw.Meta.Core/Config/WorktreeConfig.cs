namespace Aw.Meta.Core.Config;

/// <summary>How the worktree sweep behaves; every field is optional.</summary>
/// <param name="Roots">Directories whose worktrees may be removed; null means the defaults (~/repos/aw-wt, the repo's .claude/worktrees). The gate directory always counts.</param>
/// <param name="BuildOutputs">Directory names whose untracked files don't keep a worktree; null means bin, obj, artifacts, node_modules, dist.</param>
/// <param name="Enabled">Whether the triggers (merge-run, the watch) remove worktrees; false makes them only report. The explicit worktree-sweep always removes.</param>
public sealed record WorktreeConfig(IReadOnlyList<string>? Roots = null, IReadOnlyList<string>? BuildOutputs = null, bool Enabled = true)
{
    /// <summary>The build output directory names when the config gives none.</summary>
    public static IReadOnlyList<string> DefaultBuildOutputs { get; } = ["bin", "obj", "artifacts", "node_modules", "dist"];

    /// <summary>The build output directory names in force.</summary>
    public IReadOnlyList<string> EffectiveBuildOutputs => BuildOutputs ?? DefaultBuildOutputs;

    /// <summary>The roots in force: the configured ones (or the defaults), with "~" expanded, and the gate directory.</summary>
    /// <param name="repo">The team's shared checkout.</param>
    /// <param name="gatesDirectory">Where the gate trees live.</param>
    /// <param name="home">The home directory; empty when unknown (a "~" root is then left out).</param>
    /// <returns>Absolute directories.</returns>
    public IReadOnlyList<string> EffectiveRoots(string repo, string gatesDirectory, string home) => throw new NotSupportedException();
}
