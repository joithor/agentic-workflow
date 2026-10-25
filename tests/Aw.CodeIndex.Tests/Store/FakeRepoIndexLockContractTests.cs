using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Store;

[Trait("Lane", "unit")]
public sealed class FakeRepoIndexLockContractTests : RepoIndexLockContract
{
    protected override IRepoIndexLock Create() => new FakeRepoIndexLock();
}
