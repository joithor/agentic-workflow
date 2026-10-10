using Aw.Packets.App.Features.ValidatePacket;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.ValidatePacket;

public sealed class ValidatePacketTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ValidatePacket_reports_a_cycle_in_the_task_graph()
    {
        var service = new ValidatePacketService();
        var request = A.ValidatePacketRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert reports_a_cycle_in_the_task_graph");
    }
}
