using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryOwnerStoreContractTests : OwnerStoreContract
{
    protected override ValueTask<IOwnerStore> Create() => ValueTask.FromResult<IOwnerStore>(new InMemoryOwnerStore());
}
