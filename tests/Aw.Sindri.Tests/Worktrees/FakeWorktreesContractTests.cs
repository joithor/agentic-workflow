using Aw.Kernel.Testing;
using Aw.Sindri.Testing.Worktrees;

namespace Aw.Sindri.Tests.Worktrees;

[Lane(TestLane.Unit)]
public sealed class FakeWorktreesContractTests : WorktreesContract
{
    protected override ValueTask<WorktreeFixture> Create()
    {
        var one = new string('a', 40);
        var two = new string('b', 40);
        var fake = new FakeWorktrees().WithRepo("/repos/dev-repo", one, two);
        return ValueTask.FromResult(new WorktreeFixture(fake, "/repos/dev-repo", one, two, "/work/run-area", Do(fake.MakeDirty), Do(fake.AdvanceHead), Do(fake.Detach), Do(fake.DeleteDirectory)));
    }

    private static Func<string, Task> Do(Action<string> action) => path =>
    {
        action(path);
        return Task.CompletedTask;
    };
}
