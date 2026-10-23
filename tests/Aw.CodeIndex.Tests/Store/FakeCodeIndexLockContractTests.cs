using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Store;

[Trait("Lane", "unit")]
public sealed class FakeCodeIndexLockContractTests : CodeIndexLockContract
{
    protected override ICodeIndexLock Create() => new FakeCodeIndexLock();
}
