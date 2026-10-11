using System.Security.Cryptography;
using System.Text;

namespace Aw.BugFix.Infrastructure;

/// <summary>
/// The environment a test command runs with, and git's. The check gets only an allowlist of variables, so a caller can't
/// steer it through BASH_ENV, NODE_OPTIONS, exported shell functions, npm_config_*, …; of those, the ones that decide
/// which code runs (PATH, HOME, and any the user names in AW_BUGFIX_CHECK_ENV) are frozen with the check. Git runs
/// without GIT_*.
/// </summary>
internal static class CheckEnvironment
{
    /// <summary>The variables that decide which code a test command runs: frozen by value at the baseline.</summary>
    internal static readonly string[] Frozen = ["PATH", "HOME"];

    /// <summary>The variables passed through as they are (they don't change which code runs).</summary>
    internal static readonly string[] PassedThrough =
    [
        "USER", "LOGNAME", "SHELL", "TMPDIR", "TERM", "CI", "LANG", "LANGUAGE", "LC_ALL", "LC_CTYPE", "LC_MESSAGES", "LC_COLLATE",
        "LC_NUMERIC", "LC_TIME", "LC_MONETARY", "TZ",
    ];

    // Names AW_BUGFIX_CHECK_ENV may never add: each one makes an interpreter, the shell or the dynamic linker load code.
    private static readonly string[] _deniedNames =
    [
        "BASH_ENV", "ENV", "CDPATH", "PROMPT_COMMAND", "ZDOTDIR", "NODE_OPTIONS", "NODE_PATH", "RUBYOPT", "RUBYLIB", "PERL5OPT",
        "PERL5LIB", "PERLLIB", "JAVA_TOOL_OPTIONS", "_JAVA_OPTIONS", "JDK_JAVA_OPTIONS", "DOTNET_STARTUP_HOOKS", "SHELLOPTS",
        "BASHOPTS", "PYTEST_ADDOPTS", "PYTEST_PLUGINS",
    ];

    private static readonly string[] _deniedPrefixes = ["PYTHON", "LD_", "DYLD_", "BASH_FUNC_", "npm_config_", "NPM_CONFIG_", "GIT_"];

    /// <summary>Whether AW_BUGFIX_CHECK_ENV may add <paramref name="name"/>.</summary>
    /// <param name="name">A variable name.</param>
    /// <returns>False for loader and shell-startup variables.</returns>
    internal static bool MayKeep(string name) =>
        name.Length > 0
        && !_deniedNames.Contains(name, StringComparer.Ordinal)
        && !_deniedPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>The variables as digests of their values: what state and run records keep, since a value may hold a credential (DATABASE_URL).</summary>
    /// <param name="variables">The variables and their values.</param>
    /// <returns>Name → "sha256:" and the hex SHA-256 of the name, a NUL and the value.</returns>
    internal static Dictionary<string, string> Digests(IReadOnlyDictionary<string, string> variables) =>
        variables.ToDictionary(
            static kv => kv.Key,
            static kv => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{kv.Key}\0{kv.Value}"))),
            StringComparer.Ordinal);

    /// <summary>Replaces a process's environment with the given variables.</summary>
    /// <param name="environment">The process's environment.</param>
    /// <param name="variables">The variables and their values.</param>
    internal static void Only(IDictionary<string, string?> environment, IReadOnlyDictionary<string, string> variables)
    {
        environment.Clear();
        foreach (var (name, value) in variables)
        {
            environment[name] = value;
        }
    }

    /// <summary>Removes GIT_* variables (GIT_DIR, GIT_INDEX_FILE, GIT_WORK_TREE, …) so git answers about the work tree on disk.</summary>
    /// <param name="environment">The process's environment.</param>
    internal static void StripGit(IDictionary<string, string?> environment)
    {
        foreach (var key in environment.Keys.Where(static k => k.StartsWith("GIT_", StringComparison.Ordinal)).ToList())
        {
            _ = environment.Remove(key);
        }
    }
}
