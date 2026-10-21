using System.Diagnostics;
using System.Globalization;
using Xunit;

namespace Aw.CodeIndex.Testing;

/// <summary>Runs git for the fixtures: a clean environment (no GIT_* variables, no global or system config), a timeout, trimmed output.</summary>
internal static class GitRunner
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    internal static string Run(string directory, params string[] args) => RunWithInput(directory, input: null, args);

    internal static string RunWithInput(string directory, string? input, params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = input is not null, UseShellExecute = false };
        foreach (var name in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.Ordinal)).ToList())
        {
            _ = start.Environment.Remove(name);
        }

        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        foreach (var arg in (string[])["-C", directory, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false", .. args])
        {
            start.ArgumentList.Add(arg);
        }

        using var git = Process.Start(start)!;   // why: git is on the test machine's PATH
        if (input is not null)
        {
            git.StandardInput.Write(input);
            git.StandardInput.Close();
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        limit.CancelAfter(_timeout);
        var stdout = git.StandardOutput.ReadToEndAsync(limit.Token);
        var stderr = git.StandardError.ReadToEndAsync(limit.Token);
        try
        {
            Task.WaitAll([git.WaitForExitAsync(limit.Token), stdout, stderr], limit.Token);
        }
        catch (Exception e) when (!TestContext.Current.CancellationToken.IsCancellationRequested && e is OperationCanceledException or AggregateException { InnerException: OperationCanceledException })
        {
            git.Kill(entireProcessTree: true);
            throw new TimeoutException(string.Create(CultureInfo.InvariantCulture, $"git {string.Join(' ', args)} did not finish in {_timeout.TotalSeconds} s"), e);
        }

        return git.ExitCode == 0
            ? stdout.Result.Trim()
            : throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"git {string.Join(' ', args)} exited {git.ExitCode}: {stderr.Result}"));
    }
}
