using Aw.Evidence.Core.Locks;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class InMemoryMachineLocksContractTests : MachineLocksContract
{
    protected override (IMachineLocks First, IMachineLocks Second) CreatePair()
    {
        var locks = new InMemoryMachineLocks();
        return (locks, locks);
    }
}
