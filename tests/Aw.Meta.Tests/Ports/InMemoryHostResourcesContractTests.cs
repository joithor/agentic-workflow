using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryHostResourcesContractTests : HostResourcesContract
{
    protected override IHostResources Create() => new InMemoryHostResources();

    protected override void TakeLock(IHostResources resources, int pid) => ((InMemoryHostResources)resources).TakeLock(pid);
}
