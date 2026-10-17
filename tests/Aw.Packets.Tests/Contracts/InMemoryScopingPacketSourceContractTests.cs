using Aw.Kernel.Testing;
using Aw.Packets.Core.Kinds.Scoping;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class InMemoryScopingPacketSourceContractTests : ScopingPacketSourceContract
{
    protected override string Directory => "pkt";

    protected override string MissingDirectory => "missing";

    protected override IScopingPacketSource Create(IReadOnlyDictionary<string, byte[]> files) =>
        new InMemoryScopingPacketSource().With("pkt", files.ToDictionary(StringComparer.Ordinal));
}
