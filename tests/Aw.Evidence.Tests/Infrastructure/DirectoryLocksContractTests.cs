using Aw.Evidence.Core.Locks;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Integration)]
public sealed class DirectoryLocksContractTests : MachineLocksContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override (IMachineLocks First, IMachineLocks Second) CreatePair() =>
        (LockTestEnvironment.Locks(_root), LockTestEnvironment.Locks(_root));

    public void Dispose() => _root.Dispose();
}
