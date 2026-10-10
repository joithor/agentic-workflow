using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.BugFix.Core;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Testing;

/// <summary>
/// The in-memory fake for <see cref="IGit"/> and <see cref="IFiles"/>: one file system (full path → text) holding work
/// trees, and commits as snapshots of a work tree's files. They are one fake because git answers about the same files
/// the file-system port reads.
/// </summary>
public sealed class InMemoryRepos : IGit, IFiles
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _links = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Snapshot> _commits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Worktree> _worktrees = new(StringComparer.Ordinal);
    private int _next;

    /// <summary>Creates a repository at <paramref name="root"/> on branch main with one commit of <paramref name="files"/>.</summary>
    /// <param name="root">The work tree's full path.</param>
    /// <param name="files">Repository-relative path → content.</param>
    /// <returns>The first commit.</returns>
    public string Init(string root, IReadOnlyDictionary<string, string> files)
    {
        _worktrees[root] = new Worktree(root, "main", Head: string.Empty);
        foreach (var (path, text) in files)
        {
            _files[$"{root}/{path}"] = text;
        }

        return Commit(root);
    }

    /// <summary>Commits every file of the work tree (git add -A; git commit).</summary>
    /// <param name="root">The work tree.</param>
    /// <returns>The new commit.</returns>
    public string Commit(string root)
    {
        var tree = _worktrees[root];
        var sha = (++_next).ToString("x40", CultureInfo.InvariantCulture);
        _commits[sha] = new Snapshot(tree.Head, Working(root));
        _worktrees[root] = tree with { Head = sha };
        return sha;
    }

    /// <summary>Adds a work tree of the same repository on a new branch at <paramref name="commit"/> (git worktree add -b).</summary>
    /// <param name="root">The new work tree's full path.</param>
    /// <param name="branch">The new branch.</param>
    /// <param name="commit">The commit to start from.</param>
    public void AddWorktree(string root, string branch, string commit)
    {
        _worktrees[root] = new Worktree(root, branch, commit);
        foreach (var (path, text) in _commits[commit].Files)
        {
            _files[$"{root}/{path}"] = text;
        }
    }

    /// <summary>Writes a file (a working change until committed).</summary>
    /// <param name="path">The full path.</param>
    /// <param name="text">The content.</param>
    public void Write(string path, string text) => _files[path] = text;

    /// <summary>Makes <paramref name="link"/> resolve to <paramref name="target"/>, like a symlink.</summary>
    /// <param name="link">The link's full path.</param>
    /// <param name="target">The target's full path.</param>
    public void Link(string link, string target) => _links[link] = target;

    /// <inheritdoc/>
    public string FullPath(string directory, string path) =>
        path.StartsWith('/') ? Normalize(path) : Normalize($"{directory.TrimEnd('/')}/{path}");

    /// <inheritdoc/>
    public string? RealPath(string path)
    {
        var real = _links.GetValueOrDefault(path, path);
        return _files.ContainsKey(real) ? real : null;
    }

    /// <inheritdoc/>
    public Result<string> ReadText(string path) =>
        _files.TryGetValue(path, out var text) ? text : BugFixErrors.FileUnreadable.With($"cannot read {path}");

    /// <inheritdoc/>
    public Result<string> Sha256(string path) =>
        _files.TryGetValue(path, out var text)
            ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))
            : BugFixErrors.FileUnreadable.With($"cannot read {path}");

    /// <inheritdoc/>
    public Result<string> Head(string directory) => Tree(directory).Map(static t => t.Head);

    /// <inheritdoc/>
    public Result<string> Branch(string directory) => Tree(directory).Map(static t => t.Branch);

    /// <inheritdoc/>
    public Result<bool> IsClean(string directory) =>
        Tree(directory).Map(t => Same(Working(t.Root), _commits[t.Head].Files));

    /// <inheritdoc/>
    public Result<string> Root(string directory) => Tree(directory).Map(static t => t.Root);

    /// <inheritdoc/>
    public Result<bool> IsCommittedUnchanged(string root, string file) =>
        Tree(root).Map(t => _commits[t.Head].Files.TryGetValue(file, out var committed)
            && _files.TryGetValue($"{t.Root}/{file}", out var working)
            && string.Equals(committed, working, StringComparison.Ordinal));

    /// <inheritdoc/>
    public Result<bool> IsAncestor(string directory, string ancestor, string commit)
    {
        for (var at = commit; at.Length > 0; at = _commits[at].Parent)
        {
            if (string.Equals(at, ancestor, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public Result<bool> SameTree(string directory, string first, string second) =>
        Same(_commits[first].Files, _commits[second].Files);

    /// <inheritdoc/>
    public Result<IReadOnlyList<string>> ChangedFiles(string directory, string from, string to)
    {
        var (a, b) = (_commits[from].Files, _commits[to].Files);
        return Results.Success<IReadOnlyList<string>>([.. a.Keys.Union(b.Keys, StringComparer.Ordinal)
            .Where(f => !a.TryGetValue(f, out var x) || !b.TryGetValue(f, out var y) || !string.Equals(x, y, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)]);
    }

    /// <inheritdoc/>
    public Result<CommitDiff> Diff(string directory, string from, string to) =>
        ChangedFiles(directory, from, to).Map(files => new CommitDiff(
            string.Create(CultureInfo.InvariantCulture, $" {files.Count} files changed"),
            string.Concat(files.Select(f => $"diff --git a/{f} b/{f}\n+{_commits[to].Files.GetValueOrDefault(f, string.Empty)}\n"))));

    private static string Normalize(string path)
    {
        var parts = new List<string>();
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..")
            {
                parts.RemoveAt(parts.Count - 1);
            }
            else if (part != ".")
            {
                parts.Add(part);
            }
        }

        return "/" + string.Join('/', parts);
    }

    private static bool Same(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && string.Equals(v, kv.Value, StringComparison.Ordinal));

    private Result<Worktree> Tree(string directory) =>
        _worktrees.Values.FirstOrDefault(t => directory == t.Root || directory.StartsWith(t.Root + "/", StringComparison.Ordinal)) is { } tree
            ? tree
            : BugFixErrors.GitFailed.With($"fatal: not a git repository: {directory}");

    private Dictionary<string, string> Working(string root) =>
        _files.Where(kv => kv.Key.StartsWith(root + "/", StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key[(root.Length + 1)..], kv => kv.Value, StringComparer.Ordinal);

    private sealed record Snapshot(string Parent, IReadOnlyDictionary<string, string> Files);

    private sealed record Worktree(string Root, string Branch, string Head);
}
