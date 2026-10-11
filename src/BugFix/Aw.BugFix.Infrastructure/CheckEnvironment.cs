using System.Security.Cryptography;
using System.Text;

namespace Aw.BugFix.Infrastructure;

/// <summary>
/// The environment a test command runs with, and git's: the check gets only an allowlist of variables (so a caller
/// can't steer it through BASH_ENV, NODE_OPTIONS, exported shell functions, npm_config_*, …), and git runs without GIT_*.
/// </summary>
internal static class CheckEnvironment
{
    /// <summary>The variables a test command keeps from bugfix-state's own environment.</summary>
    internal static readonly string[] Allowed =
    [
        "PATH", "HOME", "USER", "LOGNAME", "SHELL", "TMPDIR", "TERM", "CI", "LANG", "LANGUAGE", "LC_ALL", "LC_CTYPE", "LC_MESSAGES",
        "LC_COLLATE", "LC_NUMERIC", "LC_TIME", "LC_MONETARY", "TZ",
    ];

    /// <summary>Replaces a process's environment with the allowed variables.</summary>
    /// <param name="environment">The process's environment.</param>
    /// <param name="allowed">The allowed variables and their values.</param>
    internal static void Only(IDictionary<string, string?> environment, IReadOnlyDictionary<string, string> allowed)
    {
        environment.Clear();
        foreach (var (name, value) in allowed)
        {
            environment[name] = value;
        }
    }

    /// <summary>The SHA-256 of the allowed variables and their values, in name order.</summary>
    /// <param name="allowed">The allowed variables.</param>
    /// <returns>Lower-case hex.</returns>
    internal static string Hash(IReadOnlyDictionary<string, string> allowed) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Concat(allowed.OrderBy(static kv => kv.Key, StringComparer.Ordinal).Select(static kv => $"{kv.Key}={kv.Value}\n")))));

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
