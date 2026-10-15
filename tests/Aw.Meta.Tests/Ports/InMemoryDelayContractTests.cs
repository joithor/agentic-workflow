using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryDelayContractTests : DelayContract
{
    protected override IDelay Create(out Func<DateTimeOffset> now)
    {
        var clock = new ManualClock(A.At(0));
        now = clock.GetUtcNow;
        return new InMemoryDelay(clock);
    }
}
