using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Aw.Kernel.Testing;

namespace Aw.CodeIndex.Tests.Store;

[Lane(TestLane.Unit)]
public sealed class FakeIndexStoreContractTests : IndexStoreContract
{
    protected override IIndexStore Create() => new FakeIndexStore();

    protected override void Corrupt(IIndexStore store, RepoName repo) => ((FakeIndexStore)store).Damage(repo);
}
