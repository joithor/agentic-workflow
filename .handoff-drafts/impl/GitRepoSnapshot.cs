using System.Globalization;
using System.Text;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Snapshot;

/// <summary>
/// The repo's tree at a commit, read with git only (spec §5.1): 'ls-tree' for the entries, 'cat-file --batch-check' for the
/// sizes (headers only, no content), then <see cref="InputRules"/> decides what to keep. The working tree is never read.
/// A promisor clone's missing blobs are counted as <see cref="SkipReason.Missing"/> and never fetched: git cannot size or
/// read an object it lacks without a fetch, so such a repo is sized from 'cat-file --batch-all-objects', which lists only
/// what is local.
/// </summary>
internal sealed class GitRepoSnapshot : IRepoSnapshot
{
    private const string Format = "--batch-check=%(objectname) %(objecttype) %(objectsize)";

    /// <inheritdoc />
    public async ValueTask<Result<SnapshotInventory>> Inventory(CodeIndexRepo repo, CommitSha commit, SnapshotLimits limits, CancellationToken cancellationToken)
    {
        var listed = await CleanGit.Run(CleanGit.Prepare(repo.Path, redirectInput: false, "ls-tree", "-r", "-z", "--full-tree", commit.Value), input: null, cancellationToken).ConfigureAwait(false);
        if (listed is not { Exit: 0 } tree)
        {
            return CodeIndexErrors.RepoNotFound.With("git could not list the commit");
        }

        var parsed = Parse(tree.Stdout);
        var sizes = await Sizes(repo.Path, parsed, cancellationToken).ConfigureAwait(false);
        if (sizes is null)
        {
            return CodeIndexErrors.RepoNotFound.With("git could not size the blobs");
        }

        return InputRules.Apply(commit, parsed.Select(e => new TreeEntry(e.Mode, e.Path, e.Blob, sizes.TryGetValue(e.Blob, out var size) ? size : null)), limits);
    }

    /// <inheritdoc />
    public async ValueTask<Result<IBlobReader>> OpenBlobs(CodeIndexRepo repo, CancellationToken cancellationToken)
    {
        var inside = await CleanGit.Run(CleanGit.Prepare(repo.Path, redirectInput: false, "rev-parse", "--git-dir"), input: null, cancellationToken).ConfigureAwait(false);
        return inside is { Exit: 0 }
            ? new GitBlobReader(repo.Path)
            : CodeIndexErrors.RepoNotFound.With("the path is not a git repo git accepts");
    }

    private static List<Listed> Parse(byte[] stdout)
    {
        var entries = new List<Listed>();
        foreach (var line in Encoding.UTF8.GetString(stdout).Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            // "<mode> SP <type> SP <object> TAB <path>"
            var tab = line.IndexOf('\t', StringComparison.Ordinal);
            if (tab > 0 && line[..tab].Split(' ') is [var mode, _, var blob])
            {
                entries.Add(new Listed(mode, line[(tab + 1)..], blob));
            }
        }

        return entries;
    }

    private static async Task<Dictionary<string, long>?> Sizes(string repo, List<Listed> entries, CancellationToken cancellationToken)
    {
        var wanted = entries.Where(e => e.Mode is "100644" or "100755").Select(e => e.Blob).Distinct(StringComparer.Ordinal).ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        var promisor = await CleanGit.Run(CleanGit.Prepare(repo, redirectInput: false, "config", "--get", "extensions.partialclone"), input: null, cancellationToken).ConfigureAwait(false);
        var start = promisor is { Exit: 0 }
            ? CleanGit.Prepare(repo, redirectInput: false, "cat-file", "--batch-all-objects", "--unordered", Format)
            : CleanGit.Prepare(repo, redirectInput: true, "cat-file", Format);
        var ran = await CleanGit.Run(start, promisor is { Exit: 0 } ? null : CleanGit.Lines(wanted), cancellationToken).ConfigureAwait(false);
        if (ran is not { Exit: 0 } sized)
        {
            return null;
        }

        var sizes = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var line in Encoding.ASCII.GetString(sized.Stdout).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // "<object> blob <size>"; "<object> missing" for an object the store lacks
            if (line.Split(' ') is [var id, "blob", var size] && long.TryParse(size, NumberStyles.None, CultureInfo.InvariantCulture, out var bytes))
            {
                sizes[id] = bytes;
            }
        }

        return sizes;
    }

    private sealed record Listed(string Mode, string Path, string Blob);
}
