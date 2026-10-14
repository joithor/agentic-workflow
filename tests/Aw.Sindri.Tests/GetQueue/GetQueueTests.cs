using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetQueue;

public sealed class GetQueueTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetQueue_groups_open_items_in_the_fixed_order_with_their_empty_text()
    {
        var service = new GetQueueService();
        var request = A.GetQueueRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert groups_open_items_in_the_fixed_order_with_their_empty_text");
    }
}
