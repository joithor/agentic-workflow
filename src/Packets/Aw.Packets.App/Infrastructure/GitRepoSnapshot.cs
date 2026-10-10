using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Packets.Core;
using Aw.Packets.Core.Ports;

namespace Aw.Packets.App.Infrastructure;

/// <summary>Lists a commit's files with the git CLI ('git ls-tree -r -z'). Every git run has a time limit.</summary>
/// <param name="timeout">How long one git run may take.</param>
internal sealed class GitRepoSnapshot(TimeSpan timeout) : IRepoSnapshot
{
    public async ValueTask<Result<IReadOnlySet<string>>> Files(string repoDirectory, string commitSha, CancellationToken cancellationToken)
    {
        var repo = await Git(repoDirectory, ["rev-parse", "--git-dir"], cancellationToken).ConfigureAwait(false);
        if (repo.IsError || repo.Value.ExitCode != 0)
        {
            return repo.IsError ? repo.Error : PacketsErrors.RepoUnavailable.With($"{repoDirectory} is not in a git repository: {repo.Value.Error}");
        }

        var commit = await Git(repoDirectory, ["rev-parse", "--verify", "--quiet", $"{commitSha}^{{commit}}"], cancellationToken).ConfigureAwait(false);
        if (commit.IsError || commit.Value.ExitCode != 0)
        {
            return commit.IsError ? commit.Error : PacketsErrors.CommitNotFound.With($"Commit {commitSha} is not in the repository at {repoDirectory}.");
        }

        var tree = await Git(repoDirectory, ["ls-tree", "-r", "-z", "--name-only", "--full-tree", commitSha], cancellationToken).ConfigureAwait(false);
        if (tree.IsError || tree.Value.ExitCode != 0)
        {
            return tree.IsError ? tree.Error : PacketsErrors.RepoUnavailable.With($"git ls-tree failed: {tree.Value.Error}");
        }

        return Results.Success<IReadOnlySet<string>>(
            tree.Value.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal));
    }

    private async Task<Result<GitRun>> Git(string directory, string[] args, CancellationToken cancellationToken)
    {
        Process process;
        try
        {
            process = Process.Start(StartInfo(directory, args))!; // why: Start returns null only when UseShellExecute reuses a process; it is off.
        }
        catch (Win32Exception ex)
        {
            return PacketsErrors.RepoUnavailable.With($"git can't run: {ex.Message}");
        }

        using (process)
        using (var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            limit.CancelAfter(timeout);
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(limit.Token);
                var error = process.StandardError.ReadToEndAsync(limit.Token);
                await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
                return new GitRun(process.ExitCode, await output.ConfigureAwait(false), (await error.ConfigureAwait(false)).Trim());
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                return PacketsErrors.RepoUnavailable.With(
                    string.Create(CultureInfo.InvariantCulture, $"git {args[0]} took longer than {timeout.TotalSeconds:0} s."));
            }
        }
    }

    private static ProcessStartInfo StartInfo(string directory, string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var arg in (string[])["-C", directory, "-c", "core.quotepath=off", .. args])
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        return start;
    }

    private sealed record GitRun(int ExitCode, string Output, string Error);
}
