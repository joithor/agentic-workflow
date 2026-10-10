using Aw.BugFix.Core;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;
using Xunit;

namespace Aw.BugFix.Testing;

/// <summary>The <see cref="IGit"/> and <see cref="IFiles"/> contract, run against the in-memory fake and the git and file-system adapters.</summary>
public abstract class ReposContract
{
    /// <summary>Gets the git port under test.</summary>
    protected abstract IGit Git { get; }

    /// <summary>Gets the file-system port under test.</summary>
    protected abstract IFiles Files { get; }

    /// <summary>Gets a directory that is in no repository.</summary>
    protected abstract string Outside { get; }

    /// <summary>Head, branch, root and cleanliness describe the work tree.</summary>
    [Fact]
    public void A_fresh_repository_is_clean_on_main_at_its_commit()
    {
        var (root, commit) = NewRepo();

        Expect.Equal(commit, Ok(Git.Head(root)), "Equal(commit, Ok(Git.Head(root)))");
        Expect.Equal("main", Ok(Git.Branch(root)), "Equal('main', Ok(Git.Branch(root)))");
        Expect.Equal(root, Ok(Git.Root($"{root}/src")), "Equal(root, Ok(Git.Root($'{root}/src')))");
        Expect.That(Ok(Git.IsClean(root)), "True(Ok(Git.IsClean(root)))");
    }

    /// <summary>A changed or untracked file makes the tree dirty and the file uncommitted.</summary>
    [Fact]
    public void Changed_and_untracked_files_are_not_clean()
    {
        var (root, _) = NewRepo();
        Write(root, "src/a.txt", "changed\n");

        Expect.That(!Ok(Git.IsClean(root)), "False(Ok(Git.IsClean(root)))");
        Expect.That(!Ok(Git.IsCommittedUnchanged(root, "src/a.txt")), "False(Ok(Git.IsCommittedUnchanged(root, 'src/a.txt')))");
        Expect.That(Ok(Git.IsCommittedUnchanged(root, "tests/t.sh")), "True(Ok(Git.IsCommittedUnchanged(root, 'tests/t.sh')))");
        Write(root, "new.txt", "x\n");
        Expect.That(!Ok(Git.IsCommittedUnchanged(root, "new.txt")), "False(Ok(Git.IsCommittedUnchanged(root, 'new.txt')))");
    }

    /// <summary>Ancestry, tree identity and changed files compare commits.</summary>
    [Fact]
    public void Commits_compare_by_ancestry_tree_and_changed_files()
    {
        var (root, first) = NewRepo();
        Write(root, "src/a.txt", "fixed\n");
        Write(root, "src/new file.txt", "new\n");
        var second = Commit(root);

        Expect.That(Ok(Git.IsAncestor(root, first, second)), "True(Ok(Git.IsAncestor(root, first, second)))");
        Expect.That(!Ok(Git.IsAncestor(root, second, first)), "False(Ok(Git.IsAncestor(root, second, first)))");
        Expect.That(!Ok(Git.SameTree(root, first, second)), "False(Ok(Git.SameTree(root, first, second)))");
        Expect.That(Ok(Git.ChangedFiles(root, first, second)).Order(StringComparer.Ordinal).SequenceEqual(["src/a.txt", "src/new file.txt"], StringComparer.Ordinal), "changed files are the two changed paths, raw");
        var diff = Ok(Git.Diff(root, first, second));
        Expect.That((diff.Full ?? string.Empty).Contains("fixed", StringComparison.Ordinal), "Contains('fixed', diff.Full, StringComparison.Ordinal)");
        Expect.That((diff.Stat ?? string.Empty).Contains("2 files changed", StringComparison.Ordinal), "Contains('2 files changed', diff.Stat, StringComparison.Ordinal)");
    }

    /// <summary>A directory outside any repository is AW-BUGFIX-018.</summary>
    [Fact]
    public void Outside_a_repository_is_AW_BUGFIX_018()
    {
        Expect.Equal(BugFixErrors.GitFailed.Code, Git.Head(Outside).Error.Code, "Equal(BugFixErrors.GitFailed.Code, Git.Head(Outside).Error.Code)");
        Expect.Equal(BugFixErrors.GitFailed.Code, Git.IsClean(Outside).Error.Code, "Equal(BugFixErrors.GitFailed.Code, Git.IsClean(Outside).Error.Code)");
    }

    /// <summary>Paths resolve against a directory, through links; a missing file has no real path and can't be read.</summary>
    [Fact]
    public void Files_resolve_paths_hash_and_read()
    {
        var (root, _) = NewRepo();

        Expect.Equal($"{root}/src/a.txt", Files.FullPath($"{root}/src", "./a.txt"), "Equal($'{root}/src/a.txt', Files.FullPath($'{root}/src', './a.txt'))");
        Expect.Equal($"{root}/tests/t.sh", Files.RealPath(Files.FullPath(root, "src/../tests/t.sh")), "Equal($'{root}/tests/t.sh', Files.RealPath(Files.FullPath(root, 'src/../tests/t.sh')))");
        Expect.That(Files.RealPath($"{root}/missing.txt") is null, "Null(Files.RealPath($'{root}/missing.txt'))");
        Expect.Equal("a\n", Ok(Files.ReadText($"{root}/src/a.txt")), "Equal('a\n', Ok(Files.ReadText($'{root}/src/a.txt')))");
        Expect.Equal("87428fc522803d31065e7bce3cf03fe475096631e5e07bbd7a0fde60c4cf25c7", Ok(Files.Sha256($"{root}/src/a.txt")), "Equal('87428fc522803d31065e7bce3cf03fe475096631e5e07bbd7a0fde60c4cf25c7', Ok(Files.Sha256($'{root}/src/a.txt')");
        Expect.Equal(BugFixErrors.FileUnreadable.Code, Files.ReadText($"{root}/missing.txt").Error.Code, "Equal(BugFixErrors.FileUnreadable.Code, Files.ReadText($'{root}/missing.txt').Error.Code)");
    }

    /// <summary>Creates a repository on main with src/a.txt ("a\n") and tests/t.sh committed.</summary>
    /// <returns>The work tree's real path and its commit.</returns>
    protected abstract (string Root, string Commit) NewRepo();

    /// <summary>Writes a file in the work tree.</summary>
    /// <param name="root">The work tree.</param>
    /// <param name="path">The repository-relative path.</param>
    /// <param name="text">The content.</param>
    protected abstract void Write(string root, string path, string text);

    /// <summary>Commits everything in the work tree.</summary>
    /// <param name="root">The work tree.</param>
    /// <returns>The new commit.</returns>
    protected abstract string Commit(string root);

    private static T Ok<T>(Result<T> result)
    {
        Expect.That(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }
}
