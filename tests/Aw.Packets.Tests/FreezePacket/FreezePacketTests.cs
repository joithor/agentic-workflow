using Aw.Packets.App.Features.FreezePacket;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.FreezePacket;

public sealed class FreezePacketTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task FreezePacket_records_the_canonical_hash_under_packets()
    {
        var service = new FreezePacketService();
        var request = A.FreezePacketRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert records_the_canonical_hash_under_packets");
    }
}
