using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Infrastructure.Store;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Store;

[Trait("Lane", "unit")]
public sealed class InProcessCodeIndexLockContractTests : CodeIndexLockContract
{
    private static readonly RepoName _repo = RepoName.From("alpha");

    protected override ICodeIndexLock Create() => new InProcessCodeIndexLock();

    [Fact]
    public async Task A_waiter_gets_the_lock_when_the_holder_releases()
    {
        var index = new InProcessCodeIndexLock();
        var first = (await index.Take(_repo, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;
        var waiting = index.Take(_repo, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken).AsTask();

        await first.DisposeAsync();
        var second = await waiting;

        Assert.True(second.IsSuccess);
        await second.Value.DisposeAsync();
    }
}
