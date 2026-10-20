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
        return ValueTask.FromResult(new WorktreeFixture(fake, "/repos/dev-repo", one, two, "/work/run-area", path => { fake.MakeDirty(path); return Task.CompletedTask; }));
    }
}
