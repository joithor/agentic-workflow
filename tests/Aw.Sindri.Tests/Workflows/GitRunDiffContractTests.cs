using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Effects;

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
}
