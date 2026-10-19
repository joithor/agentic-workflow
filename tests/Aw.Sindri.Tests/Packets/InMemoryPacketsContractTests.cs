using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Testing.Packets;

namespace Aw.Sindri.Tests.Packets;

/// <summary>The in-memory aw-packets honours the packets contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryPacketsContractTests : PacketsContract
{
    protected override IPackets Create() => new InMemoryPackets().Fails(Bad).Unreadable(Missing);
}
