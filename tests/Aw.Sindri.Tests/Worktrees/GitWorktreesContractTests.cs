using Aw.Kernel.Testing;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Infrastructure.Worktrees;
using Aw.Sindri.Testing.Worktrees;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Worktrees;

/// <summary>The git adapter against a real temp repo with two commits.</summary>
[Lane(TestLane.Integration)]
public sealed class GitWorktreesContractTests : WorktreesContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];
    private readonly List<string> _parents = [];

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }

        foreach (var parent in _parents.Where(Directory.Exists))
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    /// <summary>A refused remove leaves the uncommitted file where it was.</summary>
    [Fact]
    public async Task A_dirty_remove_leaves_the_files_on_disk()
    {
        var f = await Create();
        var made = await f.Worktrees.Create(new WorktreeRequest(f.RepoRoot, f.Base, "sindri/run-1", Path.Combine(f.Parent, "run-1")), TestContext.Current.CancellationToken);
        await f.MakeDirty(made.Value.Path);

        _ = await f.Worktrees.Remove(made.Value.Path, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(made.Value.Path, "scratch.txt")));
    }

    /// <summary>Removing a gone directory prunes its registration, so the repo no longer lists it.</summary>
    [Fact]
    public async Task Removing_a_gone_directory_prunes_the_registration()
    {
        var f = await Create();
        var made = await f.Worktrees.Create(new WorktreeRequest(f.RepoRoot, f.Base, "sindri/run-1", Path.Combine(f.Parent, "run-1")), TestContext.Current.CancellationToken);
        await f.DeleteDirectory(made.Value.Path);

        _ = await f.Worktrees.Remove(made.Value.Path, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("run-1", await _repos[0].Git("worktree", "list", "--porcelain"), StringComparison.Ordinal);
    }

    protected override async ValueTask<WorktreeFixture> Create()
    {
        var repo = await TempGitRepo.Create(2);
        _repos.Add(repo);
        var parent = Path.Combine(Path.GetTempPath(), "aw-wt-" + Path.GetRandomFileName());
        _ = Directory.CreateDirectory(parent);
        _parents.Add(parent);
        var first = await repo.Git("rev-parse", "HEAD~1");
        var second = await repo.Git("rev-parse", "HEAD");
        return new WorktreeFixture(new GitWorktrees(), repo.Path, first, second, parent, path => File.WriteAllTextAsync(Path.Combine(path, "scratch.txt"), "x", TestContext.Current.CancellationToken),
            path => TempGitRepo.GitIn(path, "-c", "user.email=test@example.invalid", "-c", "user.name=test", "commit", "-q", "--allow-empty", "-m", "more"),
            path => TempGitRepo.GitIn(path, "checkout", "-q", "--detach"),
            path =>
            {
                Directory.Delete(path, recursive: true);
                return Task.CompletedTask;
            });
    }
}
