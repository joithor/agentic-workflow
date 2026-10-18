namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The sandbox's git settings, delivered at command-line scope so they beat the repo's own config (sandbox spec §3.4).</summary>
public static class GitSettings
{
    /// <summary>The settings for one launch.</summary>
    /// <param name="worktree">The worktree ('safe.directory'), or null.</param>
    /// <param name="hooksDir">The per-launch directory 'core.hooksPath' points at.</param>
    /// <returns>The settings, in order.</returns>
    public static IReadOnlyList<GitSetting> For(string? worktree, string hooksDir) => throw new NotSupportedException();

    /// <summary>The environment that delivers the settings: GIT_CONFIG_COUNT, GIT_CONFIG_KEY_n, GIT_CONFIG_VALUE_n and GIT_CONFIG_GLOBAL.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="emptyGlobalFile">The path of an empty file for GIT_CONFIG_GLOBAL.</param>
    /// <returns>The variables.</returns>
    public static IReadOnlyList<PlanEnvVar> ToEnvironment(IReadOnlyList<GitSetting> settings, string emptyGlobalFile) => throw new NotSupportedException();
}
