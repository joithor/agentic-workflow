using Aw.Kernel.Testing;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class InMemoryLinePacketSourceContractTests : LinePacketSourceContract
{
    protected override string Directory => "pkt";

    protected override string MissingDirectory => "missing";

    protected override ILinePacketSource Create(IReadOnlyDictionary<string, byte[]> files) =>
        new InMemoryLinePacketSource().With("pkt", files.ToDictionary(StringComparer.Ordinal));
}
