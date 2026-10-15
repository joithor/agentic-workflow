using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryKeyValueStoreContractTests : KeyValueStoreContract
{
    protected override ValueTask<IKeyValueStore> Create() => ValueTask.FromResult<IKeyValueStore>(new InMemoryKeyValueStore());
}
