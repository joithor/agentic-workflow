namespace Aw.Sindri.Core.Launch;

/// <summary>What a launched agent Step's environment is scrubbed of and given (line spec LN-LCH1): a Step never holds a forge credential, so it can't push; Sindri pushes.</summary>
public static class LaunchEnvironment
{
    /// <summary>The variable naming the Step's worktree; the pre-push hook the launcher installs refuses only where it is set.</summary>
    public const string Worktree = "AW_SINDRI_WORKTREE";

    /// <summary>The forge credentials a Step must not hold: the GitHub CLI's token, the same for Actions and the API, the Enterprise token, and the forge port's default credential variable.</summary>
    public static IReadOnlyList<string> Credentials { get; } = ["GH_TOKEN", "GITHUB_TOKEN", "GH_ENTERPRISE_TOKEN", "AW_FORGE_TOKEN"];

    /// <summary>The variable the GitHub CLI reads its config directory from.</summary>
    public const string GhConfigDirectory = "GH_CONFIG_DIR";

    /// <summary>
    /// <paramref name="env"/> scrubbed for a Step: each credential variable is set to the empty string (the host only adds variables to the session it starts,
    /// so a variable can't be removed; an empty token is no token to `gh` and `git`), <see cref="GhConfigDirectory"/> points at an empty directory
    /// and <see cref="Worktree"/> names the Step's worktree.
    /// </summary>
    /// <param name="env">The variables the launch already sets.</param>
    /// <param name="worktree">The Step's worktree (the phase worktree, else its working directory).</param>
    /// <param name="ghConfigDir">An empty directory for the GitHub CLI's config.</param>
    /// <returns>A new dictionary; <paramref name="env"/> is not changed.</returns>
    public static IReadOnlyDictionary<string, string> Scrub(IReadOnlyDictionary<string, string> env, string worktree, string ghConfigDir)
    {
        ArgumentNullException.ThrowIfNull(env);
        Dictionary<string, string> scrubbed = new(env, StringComparer.Ordinal);
        foreach (var name in Credentials)
        {
            scrubbed[name] = string.Empty;
        }

        scrubbed[GhConfigDirectory] = ghConfigDir;
        scrubbed[Worktree] = worktree;
        return scrubbed;
    }
}
