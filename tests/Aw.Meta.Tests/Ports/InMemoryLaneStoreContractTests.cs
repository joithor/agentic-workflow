using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryLaneStoreContractTests : LaneStoreContract
{
    protected override ValueTask<ILaneStore> Create() => ValueTask.FromResult<ILaneStore>(new InMemoryLaneStore());
}
