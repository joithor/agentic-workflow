using System.Diagnostics;

namespace Aw.Context.Tests.Eval.Backtest;

/// <summary>Runs git in a fixture checkout with the caller's GIT_* variables removed.</summary>
internal static class GitHelper
{
    public static string Run(string repo, params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var name in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.Ordinal)).ToList())
        {
            _ = start.Environment.Remove(name);
        }

        foreach (var arg in (string[])["-C", repo, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false", .. args])
        {
            start.ArgumentList.Add(arg);
        }

        using var git = Process.Start(start)!;   // why: git is on the test machine's PATH
        var output = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        return output;
    }
}
