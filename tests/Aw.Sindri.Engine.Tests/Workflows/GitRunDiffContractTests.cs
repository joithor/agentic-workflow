using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflows.Effects;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The git adapter of the run diff against a real temp repo with one commit.</summary>
[Lane(TestLane.Integration)]
public sealed class GitRunDiffContractTests : RunDiffContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    protected override async ValueTask<(IRunDiff Diff, string Worktree)> Create()
    {
        var repo = await TempGitRepo.Create();
        _repos.Add(repo);
        return (new GitRunDiff(), repo.Path);
    }

    protected override async ValueTask WriteFile(string worktree) =>
        await File.WriteAllTextAsync(Path.Combine(worktree, "new.txt"), "changed", TestContext.Current.CancellationToken);

    protected override async ValueTask Commit(string worktree) =>
        await _repos.Single(r => string.Equals(r.Path, worktree, StringComparison.Ordinal)).Git("-c", "user.email=test@example.invalid", "-c", "user.name=test", "commit", "-q", "--allow-empty", "-m", "more");

    [Fact]
    public async Task A_committed_staged_edited_and_new_file_are_all_changed_paths_once_each_in_order()
    {
        var (diff, worktree) = await Create();
        var repo = _repos.Single(r => string.Equals(r.Path, worktree, StringComparison.Ordinal));
        var start = await diff.Head(worktree, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(worktree, "b.md"), "b", TestContext.Current.CancellationToken);
        _ = await repo.Git("add", "b.md");
        _ = await repo.Git("-c", "user.email=test@example.invalid", "-c", "user.name=test", "commit", "-q", "-m", "b");
        await File.WriteAllTextAsync(Path.Combine(worktree, "a.md"), "a", TestContext.Current.CancellationToken);
        _ = await repo.Git("add", "a.md");
        await File.AppendAllTextAsync(Path.Combine(worktree, "b.md"), " more", TestContext.Current.CancellationToken);
        await WriteFile(worktree);

        var paths = await diff.ChangedPaths(worktree, start.Value, TestContext.Current.CancellationToken);

        Assert.Equal(["a.md", "b.md", "new.txt"], paths.Value);
    }
}
