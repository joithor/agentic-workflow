using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class FakeRunDiffContractTests : RunDiffContract
{
    private const string Tree = "/work/tree";

    private readonly FakeRunDiff _diff = new();

    protected override ValueTask<(IRunDiff Diff, string Worktree)> Create()
    {
        _diff.Add(Tree);
        return ValueTask.FromResult<(IRunDiff, string)>((_diff, Tree));
    }

    protected override ValueTask WriteFile(string worktree)
    {
        _diff.Touch(worktree, "new.txt");
        return ValueTask.CompletedTask;
    }

    protected override ValueTask Commit(string worktree)
    {
        _diff.Advance(worktree);
        return ValueTask.CompletedTask;
    }
}
