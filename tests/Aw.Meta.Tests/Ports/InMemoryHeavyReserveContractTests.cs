using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryHeavyReserveContractTests : HeavyReserveContract
{
    protected override IHeavyReserve Create() => new InMemoryHeavyReserve();

    protected override void SetCap(IHeavyReserve reserve, int cap) => ((InMemoryHeavyReserve)reserve).Cap = cap;
}
