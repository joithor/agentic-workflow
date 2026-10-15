using Aw.Context.Core.Locks;
using Aw.Context.Testing.Locks;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeHeavyJobLockContractTests : HeavyJobLockContract
{
    protected override IHeavyJobLock CreateLock(string? busyHolder, int capacity) => new FakeHeavyJobLock { BusyHolder = busyHolder };
}
