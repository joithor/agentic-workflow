using System.Diagnostics;
using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Snapshot;

/// <summary>The repo's tree at a commit, read with git only (spec §5.1).</summary>
internal sealed class GitRepoSnapshot : IRepoSnapshot
{
    private static readonly TimeSpan _listTimeout = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    public async ValueTask<Result<SnapshotInventory>> Inventory(CodeIndexRepo repo, CommitSha commit, SnapshotLimits limits, CancellationToken cancellationToken)
    {
        var listed = await Run(repo.Path, cancellationToken, "ls-tree", "-r", "-z", "--full-tree", "--long", commit.Value).ConfigureAwait(false);
        return listed.IsError ? listed.Error : InputRules.Apply(commit, Parse(listed.Value), limits);
    }

    /// <inheritdoc />
    public async ValueTask<Result<IBlobReader>> OpenBlobs(CodeIndexRepo repo, CancellationToken cancellationToken)
    {
        var check = await Run(repo.Path, cancellationToken, "rev-parse", "--git-dir").ConfigureAwait(false);
        return check.IsError ? check.Error : GitBlobReader.Start(repo.Path);
    }

    private static async ValueTask<Result<string>> Run(string repo, CancellationToken cancellationToken, params string[] args)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_listTimeout);
        Process? git = null;
        try
        {
            git = Process.Start(CleanGit.Prepare(repo, redirectInput: false, args));
            if (git is null)
            {
                return CodeIndexErrors.RepoNotFound.With("git did not start");
            }

            var stdout = ReadAll(git.StandardOutput, timeout.Token);
            var stderr = git.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
            await git.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var text = await stdout.ConfigureAwait(false);
            await stderr.ConfigureAwait(false);
            return git.ExitCode == 0
                ? text
                : CodeIndexErrors.RepoNotFound.With(string.Create(CultureInfo.InvariantCulture, $"git {args[0]} exited {git.ExitCode}"));
        }
        catch (OperationCanceledException)
        {
            Kill(git);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return CodeIndexErrors.RepoNotFound.With($"git {args[0]} did not finish in time");
        }
        finally
        {
            git?.Dispose();
        }
    }

    private static async Task<string> ReadAll(StreamReader reader, CancellationToken cancellationToken) =>
        await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

    private static void Kill(Process? git)
    {
        try
        {
            if (git is { HasExited: false })
            {
                git.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // it exited between the check and the kill
        }
    }

    private static IEnumerable<TreeEntry> Parse(string output)
    {
        foreach (var record in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            // "<mode> SP <type> SP <object> SP <size, padded> TAB <path>"
            var tab = record.IndexOf('\t', StringComparison.Ordinal);
            if (tab < 0)
            {
                continue;
            }

            var meta = record[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (meta.Length != 4 || meta[1] == "tree")
            {
                continue;
            }

            long? size = long.TryParse(meta[3], NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) ? bytes : null;
            yield return new TreeEntry(meta[0], record[(tab + 1)..], meta[2], meta[1] == "blob" ? size : 0);
        }
    }
}
