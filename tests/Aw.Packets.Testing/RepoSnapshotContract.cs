using Aw.Packets.Core;
using Aw.Packets.Core.Values;
using Xunit;

namespace Aw.Packets.Testing;

/// <summary>The IRepoSnapshot contract, run against the in-memory fake and the git adapter.</summary>
public abstract class RepoSnapshotContract
{
    /// <summary>The files at a commit, repo-relative with '/'.</summary>
    protected static readonly IReadOnlySet<string> CommitFiles = new HashSet<string>(StringComparer.Ordinal)
    {
        "README.md", "src/a.cs", "src/deep/b c.cs", "docs/ünïcode.md",
    };

    /// <summary>A commit lists exactly its files.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_commit_lists_exactly_its_files()
    {
        var (snapshot, repo, sha) = Create();

        var files = await snapshot.Files(repo, CommitSha.From(sha), CancellationToken.None);

        Expect.That(files.IsSuccess && files.Value.SetEquals(CommitFiles), "the commit's files, every one, with '/' separators");
    }

    /// <summary>A commit the repository doesn't have is AW-PACKETS-005.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unknown_commit_is_AW_PACKETS_005()
    {
        var (snapshot, repo, _) = Create();

        var files = await snapshot.Files(repo, CommitSha.From(new string('e', 40)), CancellationToken.None);

        Expect.That(files.IsError && files.Error.Code == PacketsErrors.CommitNotFound.Code, "an unknown commit is AW-PACKETS-005");
    }

    /// <summary>A directory that isn't a repository is AW-PACKETS-006.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Not_a_repository_is_AW_PACKETS_006()
    {
        var (snapshot, _, sha) = Create();

        var files = await snapshot.Files(NotARepository, CommitSha.From(sha), CancellationToken.None);

        Expect.That(files.IsError && files.Error.Code == PacketsErrors.RepoUnavailable.Code, "a directory outside any repository is AW-PACKETS-006");
    }

    /// <summary>A directory that isn't inside any repository.</summary>
    protected abstract string NotARepository { get; }

    /// <summary>Creates the snapshot with one commit holding <see cref="CommitFiles"/>.</summary>
    /// <returns>The snapshot, a directory in the repository, and the commit's sha.</returns>
    protected abstract (IRepoSnapshot Snapshot, string Repo, string Sha) Create();
}
