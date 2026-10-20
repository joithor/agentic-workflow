using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Runs git for the engine's adapters (the claim ref, the run diff): an argument list, never a shell string, each call bounded to 30 s and never left running.</summary>
internal static class GitProcess
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(30);

    internal static async Task<GitRun> Run(string repository, string[] args, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("git", ["-C", repository, .. args])
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["LC_ALL"] = "C";   // git's messages are matched in English
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";   // never wait for a credential
        Process? git;
        try
        {
            git = Process.Start(start);
        }
        catch (Win32Exception e) // boundary: no git on PATH is an expected failure the activity retries
        {
            return new GitRun(-1, string.Empty, $"git can't run: {e.Message}");
        }

        using (git)
        {
            return git is null ? new GitRun(-1, string.Empty, "git didn't start") : await Answer(git, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<GitRun> Answer(Process git, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_budget);
        try
        {
            var output = git.StandardOutput.ReadToEndAsync(deadline.Token);
            var error = git.StandardError.ReadToEndAsync(deadline.Token);
            await git.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            return new GitRun(git.ExitCode, (await output.ConfigureAwait(false)).Trim(), await error.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            git.Kill();   // the budget or the caller's cancellation: never leave git running
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new GitRun(-1, string.Empty, "git didn't answer within 30 s");
        }
    }
}
