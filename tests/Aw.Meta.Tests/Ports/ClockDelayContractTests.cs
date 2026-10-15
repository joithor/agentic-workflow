using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class ClockDelayContractTests : DelayContract
{
    protected override IDelay Create(out Func<DateTimeOffset> now)
    {
        now = TimeProvider.System.GetUtcNow;
        return new ClockDelay(TimeProvider.System);
    }
}
