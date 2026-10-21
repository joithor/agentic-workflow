using System.Diagnostics;

namespace Aw.CodeIndex.Infrastructure.Snapshot;

/// <summary>How the snapshot starts git (spec §5.1): no inherited GIT_* state, no config but the command line's, no network, no replace refs.</summary>
internal static class CleanGit
{
    /// <summary>Removes every GIT_* variable and sets the hardening ones.</summary>
    /// <param name="environment">The process environment to change.</param>
    internal static void ScrubEnvironment(IDictionary<string, string?> environment)
    {
        foreach (var name in environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _ = environment.Remove(name);
        }

        environment["GIT_OPTIONAL_LOCKS"] = "0";
        environment["GIT_NO_LAZY_FETCH"] = "1";
        environment["GIT_CONFIG_NOSYSTEM"] = "1";
        environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
    }

    /// <summary>The arguments that come before the git verb.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <returns>The global arguments.</returns>
    internal static IReadOnlyList<string> GlobalArguments(string repo) =>
    [
        "-C", repo, "--no-replace-objects",
        "-c", "protocol.allow=never", "-c", "core.fsmonitor=false", "-c", "core.hooksPath=/dev/null", "-c", "core.quotepath=off",
    ];

    /// <summary>Prepares a git process; the caller starts it.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="redirectInput">Whether the caller writes to git's standard input.</param>
    /// <param name="args">The verb and its arguments.</param>
    /// <returns>The start info.</returns>
    internal static ProcessStartInfo Prepare(string repo, bool redirectInput, params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = redirectInput, UseShellExecute = false };
        ScrubEnvironment(start.Environment);
        foreach (var arg in GlobalArguments(repo).Concat(args))
        {
            start.ArgumentList.Add(arg);
        }

        return start;
    }
}
