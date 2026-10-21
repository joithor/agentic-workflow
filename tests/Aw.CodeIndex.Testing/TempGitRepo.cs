using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Testing;

/// <summary>A throwaway git repo made by <see cref="GitRepoBuilder"/>; Dispose deletes it (and the origin of a promisor clone).</summary>
public sealed class TempGitRepo : IDisposable
{
    internal TempGitRepo(string path, string? origin = null)
    {
        Path = path;
        Origin = origin;
    }

    /// <summary>The checkout's absolute path.</summary>
    public string Path { get; }

    /// <summary>The origin a promisor clone was made from, or null.</summary>
    public string? Origin { get; }

    /// <summary>The checkout's HEAD commit.</summary>
    public CommitSha Head => CommitSha.From(Git("rev-parse", "HEAD"));

    /// <summary>This repo as the facade sees a registered repo.</summary>
    /// <param name="name">Its name; defaults to 'fixture'.</param>
    /// <returns>The repo, with default settings.</returns>
    public CodeIndexRepo AsRepo(string name = "fixture") => new(RepoName.From(name), Path, Core.CodeIndexSettings.Default);

    /// <summary>Runs git in the checkout.</summary>
    /// <param name="args">The git arguments.</param>
    /// <returns>Git's trimmed output; throws when git fails.</returns>
    public string Git(params string[] args) => GitRunner.Run(Path, args);

    /// <summary>Writes a file in the working tree (not staged).</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="text">The content.</param>
    public void WriteFile(string path, string text)
    {
        var full = System.IO.Path.Combine(Path, path);
        _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    /// <summary>Stages everything and commits it.</summary>
    /// <param name="message">The commit message.</param>
    /// <returns>The new commit.</returns>
    public CommitSha Commit(string message)
    {
        _ = Git("add", "-A");
        _ = Git("commit", "-q", "--allow-empty", "-m", message);
        return Head;
    }

    /// <summary>Replaces a tracked regular file in the working tree by a symlink, after the commit: the tree still holds the file (spec §5.1: a path swapped after the commit does not matter).</summary>
    /// <param name="path">The tracked path.</param>
    /// <param name="target">What the symlink points at.</param>
    public void SwapWithSymlink(string path, string target)
    {
        var full = System.IO.Path.Combine(Path, path);
        File.Delete(full);
        _ = File.CreateSymbolicLink(full, target);
    }

    /// <summary>
    /// Commits, on top of HEAD, a directory whose files carry names git's own checks refuse to stage ('..', '.GIT', a backslash),
    /// written as raw tree entries with 'git mktree'. The names cannot be made by 'git add' or on every file system, which is
    /// how a hostile repo would carry them (spec §5.1 unsafe-path).
    /// </summary>
    /// <param name="directory">The one directory (a plain name) that holds them.</param>
    /// <param name="names">The file names, each a single path component.</param>
    /// <returns>The new commit.</returns>
    public CommitSha CommitUnsafeNames(string directory, params string[] names)
    {
        var blob = GitRunner.RunWithInput(Path, "unsafe\n", "hash-object", "-w", "--stdin");
        var inner = GitRunner.RunWithInput(Path, string.Concat(names.Select(n => $"100644 blob {blob}\t{n}\n")), "mktree");
        var top = GitRunner.RunWithInput(Path, Git("ls-tree", "HEAD") + $"\n040000 tree {inner}\t{directory}\n", "mktree");
        var commit = Git("-c", "core.hooksPath=/dev/null", "commit-tree", "-p", "HEAD", "-m", "unsafe names", top);
        _ = Git("update-ref", "HEAD", commit);
        return Head;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DeleteTree(Path);
        if (Origin is not null)
        {
            DeleteTree(Origin);
        }
    }

    private static void DeleteTree(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a cleanup that fails must not replace the test's own failure
        }
    }
}
