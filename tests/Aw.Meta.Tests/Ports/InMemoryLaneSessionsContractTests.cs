using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryLaneSessionsContractTests : LaneSessionsContract
{
    protected override (ILaneSessions Sessions, Action<string, int, string> Start) Create()
    {
        var fake = new InMemoryLaneSessions();
        return (fake, fake.Start);
    }
}
