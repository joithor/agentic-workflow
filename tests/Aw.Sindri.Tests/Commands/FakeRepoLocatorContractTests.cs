using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

[Lane(TestLane.Unit)]
public sealed class FakeRepoLocatorContractTests : RepoLocatorContract
{
    private readonly FakeRepoLocator _locator = new();

    protected override IRepoLocator Create() => _locator;

    protected override ValueTask<(string MainCheckout, string InWorktree)> RepoWithWorktree()
    {
        _ = _locator.In("/work/demo/.claude/worktrees/x/src", "/work/demo");
        return ValueTask.FromResult(("/work/demo", "/work/demo/.claude/worktrees/x/src"));
    }

    protected override ValueTask<string> OutsideGit() => ValueTask.FromResult("/elsewhere");
}
