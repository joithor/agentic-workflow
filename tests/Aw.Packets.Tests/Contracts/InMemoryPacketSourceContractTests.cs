using Aw.Kernel.Testing;
using Aw.Packets.Core;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class InMemoryPacketSourceContractTests : PacketSourceContract
{
    protected override string Directory => "pkt";

    protected override string MissingDirectory => "missing";

    protected override IPacketSource Create(IReadOnlyDictionary<string, byte[]> files) =>
        new InMemoryPacketSource().With("pkt", files.ToDictionary(StringComparer.Ordinal));
}
