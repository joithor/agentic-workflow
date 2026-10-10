using Aw.Packets.App.Features.ValidatePacket;
using Aw.Packets.Core.Checks;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.ValidatePacket;

public sealed class ValidatePacketTests
{
    private static ValidatePacketService Service(PacketBuilder packet) => new(new PacketInspector(
        new InMemoryPacketSource().With("pkt", PacketFiles.Of(packet.Build())),
        new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles)));

    [Fact, Trait("Lane", "unit")]
    public async Task ValidatePacket_reports_a_cycle_in_the_task_graph()
    {
        var service = Service(A.Packet()
            .With(A.Task("t1").After("t3").Writing("src/a.cs"))
            .With(A.Task("t2").After("t1").Writing("src/a.cs"))
            .With(A.Task("t3").After("t2").Writing("src/a.cs")));

        var result = await service.Handle(new ValidatePacketRequest("pkt"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Passed);
        var cycle = Assert.Single(result.Value.Checks, static c => c.Rule == "PKT-GRAPH-002" && c.State == CheckState.Fail);
        Assert.Contains("t1 -> t3 -> t2 -> t1", cycle.Message, StringComparison.Ordinal);
    }
}
