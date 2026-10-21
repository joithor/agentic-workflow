using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The Step's worktree against a real temp repo.</summary>
[Lane(TestLane.Integration)]
public sealed class LaunchWorktreeTests : IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    [Fact]
    public async Task A_missing_worktree_is_created_and_registered()
    {
        var repo = await Repo();
        var worktree = Path.Combine(repo.Path, ".claude", "worktrees", "step-1");

        var error = await LaunchWorktree.Ensure(A.LaunchRequest().Build() with { RepoPath = repo.Path, Worktree = worktree }, TestContext.Current.CancellationToken);

        Assert.Null(error);
        Assert.Contains(worktree, await repo.Git("worktree", "list", "--porcelain"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_add_is_SND_LAUNCH_004_and_leaves_no_worktree_registered()
    {
        var repo = await Repo();
        var blocker = Path.Combine(repo.Path, "not-a-directory");
        await File.WriteAllTextAsync(blocker, "x", TestContext.Current.CancellationToken);
        var worktree = Path.Combine(blocker, "step-1");

        var error = await LaunchWorktree.Ensure(A.LaunchRequest().Build() with { RepoPath = repo.Path, Worktree = worktree }, TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.LaunchRepoMissing.Code, error?.Code);
        Assert.DoesNotContain("step-1", await repo.Git("worktree", "list", "--porcelain"), StringComparison.Ordinal);
    }

    private async Task<TempGitRepo> Repo()
    {
        var repo = await TempGitRepo.Create();
        _repos.Add(repo);
        return repo;
    }
}
