using Aw.Kernel.Testing;
using Aw.Packets.Core;
using Aw.Packets.Core.Model;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class InMemoryFreezeLogContractTests : FreezeLogContract
{
    protected override IFreezeLog Create() => new InMemoryFreezeLog();

    protected override IFreezeLog CreateUnwritable() => new InMemoryFreezeLog { Unwritable = true };

    protected override IReadOnlyList<FreezeRecord> ReadBack(IFreezeLog log) => [.. ((InMemoryFreezeLog)log).Records.Values];
}
