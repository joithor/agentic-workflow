using Aw.Kernel.Testing;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

[Lane(TestLane.Unit)]
public sealed class FakeHostProbesContractTests : HostProbesContract
{
    protected override Scene Create(out IDisposable holder)
    {
        var probes = new FakeHostProbes();
        _ = probes.Directories.Add("/work");
        _ = probes.TakenPorts.Add(4100);
        holder = new NothingToRelease();
        return new Scene(TakenPort: 4100, FreePort: 4101, Directory: "/work", probes);
    }

    private sealed class NothingToRelease : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
