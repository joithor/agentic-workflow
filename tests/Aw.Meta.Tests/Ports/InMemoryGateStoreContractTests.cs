using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryGateStoreContractTests : GateStoreContract
{
    protected override ValueTask<IGateStore> Create() => ValueTask.FromResult<IGateStore>(new InMemoryGateStore());
}
