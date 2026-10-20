using System.Security.Cryptography;
using System.Text;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Testing;

/// <summary>
/// An in-memory tree at a commit (spec §11). Stub level: it keeps regular files, skips symlinks and submodules (counted),
/// applies the size caps and a path being absent from the object store ('missing'); the deny globs and path validation
/// are InputRules' (CI-2), which will join this fake and RepoSnapshotContract.
/// </summary>
public sealed class FakeRepoSnapshot : IRepoSnapshot
{
    private readonly Dictionary<CommitSha, List<Entry>> _commits = [];
    private readonly Dictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missing = new(StringComparer.Ordinal);

    /// <summary>Adds a regular file to a commit's tree.</summary>
    /// <param name="commit">The commit.</param>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="text">The content.</param>
    /// <returns>This fake.</returns>
    public FakeRepoSnapshot WithFile(CommitSha commit, string path, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var blob = BlobId(bytes);
        _blobs[blob] = bytes;
        Tree(commit).Add(new Entry(path, blob, bytes.Length, Skip: null));
        return this;
    }

    /// <summary>Adds a symlink or submodule entry to a commit's tree; the snapshot skips and counts it.</summary>
    /// <param name="commit">The commit.</param>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="skip">Symlink or Submodule.</param>
    /// <returns>This fake.</returns>
    public FakeRepoSnapshot WithSkipped(CommitSha commit, string path, SkipReason skip)
    {
        Tree(commit).Add(new Entry(path, Blob: string.Empty, Size: 0, skip));
        return this;
    }

    /// <summary>Removes a blob from the object store: the entry stays in the tree, its read answers null (a promisor clone's missing blob).</summary>
    /// <param name="commit">The commit whose tree holds the file.</param>
    /// <param name="path">The path.</param>
    /// <returns>This fake.</returns>
    public FakeRepoSnapshot WithMissingBlob(CommitSha commit, string path)
    {
        _ = _missing.Add(Tree(commit).Single(e => string.Equals(e.Path, path, StringComparison.Ordinal)).Blob);
        return this;
    }

    /// <inheritdoc />
    public ValueTask<Result<SnapshotInventory>> Inventory(CodeIndexRepo repo, CommitSha commit, SnapshotLimits limits, CancellationToken cancellationToken)
    {
        if (!_commits.TryGetValue(commit, out var tree))
        {
            return ValueTask.FromResult<Result<SnapshotInventory>>(CodeIndexErrors.RepoNotFound.With("the commit is not in the repo"));
        }

        var skipped = new Dictionary<SkipReason, int>();
        var kept = new List<SnapshotFile>();
        foreach (var entry in tree.OrderBy(e => e.Path, StringComparer.Ordinal))
        {
            var reason = entry.Skip ?? (entry.Size > limits.MaxFileBytes ? SkipReason.TooLarge : null);
            if (reason is { } why)
            {
                skipped[why] = skipped.GetValueOrDefault(why) + 1;
                continue;
            }

            kept.Add(new SnapshotFile(entry.Path, entry.Blob, entry.Size));
        }

        if (kept.Sum(f => f.Size) > limits.MaxTotalBytes)
        {
            return ValueTask.FromResult<Result<SnapshotInventory>>(CodeIndexErrors.InputTooLarge.With("the kept files are over the total size cap"));
        }

        return ValueTask.FromResult<Result<SnapshotInventory>>(new SnapshotInventory(commit, kept, skipped));
    }

    /// <inheritdoc />
    public ValueTask<Result<IBlobReader>> OpenBlobs(CodeIndexRepo repo, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<IBlobReader>>(new Reader(this));

    private static string BlobId(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes))[..40];

    private List<Entry> Tree(CommitSha commit)
    {
        if (!_commits.TryGetValue(commit, out var tree))
        {
            tree = [];
            _commits[commit] = tree;
        }

        return tree;
    }

    private sealed record Entry(string Path, string Blob, long Size, SkipReason? Skip);

    private sealed class Reader(FakeRepoSnapshot owner) : IBlobReader
    {
        public ValueTask<Result<byte[]?>> Read(string blob, CancellationToken cancellationToken)
            => ValueTask.FromResult<Result<byte[]?>>(!owner._missing.Contains(blob) && owner._blobs.TryGetValue(blob, out var bytes) ? bytes : null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
