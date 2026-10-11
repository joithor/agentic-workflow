namespace Aw.BugFix.Infrastructure;

/// <summary>
/// The environment variables a caller could use to change what a test command or git does without changing a committed
/// file: shell startup files, interpreter preloads, dynamic-linker injection, and git's location overrides.
/// </summary>
internal static class CheckEnvironment
{
    private static readonly string[] _names =
    [
        "BASH_ENV", "ENV", "CDPATH", "NODE_OPTIONS", "NODE_PATH", "RUBYOPT", "RUBYLIB", "PERL5OPT", "PERL5LIB", "PERLLIB",
        "JAVA_TOOL_OPTIONS", "_JAVA_OPTIONS", "JDK_JAVA_OPTIONS", "DOTNET_STARTUP_HOOKS", "PROMPT_COMMAND", "ZDOTDIR",
    ];

    private static readonly string[] _prefixes = ["PYTHON", "LD_", "DYLD_"];

    /// <summary>Removes the loader variables from a process's environment.</summary>
    /// <param name="environment">The process's environment.</param>
    internal static void StripLoaders(IDictionary<string, string?> environment) =>
        Strip(environment, key => _names.Contains(key, StringComparer.Ordinal) || _prefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal)));

    /// <summary>Removes GIT_* variables (GIT_DIR, GIT_INDEX_FILE, GIT_WORK_TREE, …) so git answers about the work tree on disk.</summary>
    /// <param name="environment">The process's environment.</param>
    internal static void StripGit(IDictionary<string, string?> environment) =>
        Strip(environment, static key => key.StartsWith("GIT_", StringComparison.Ordinal));

    private static void Strip(IDictionary<string, string?> environment, Func<string, bool> remove)
    {
        foreach (var key in environment.Keys.Where(remove).ToList())
        {
            _ = environment.Remove(key);
        }
    }
}
