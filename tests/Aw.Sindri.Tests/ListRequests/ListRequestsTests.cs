using Aw.Sindri.Features.ListRequests;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ListRequests;

public sealed class ListRequestsTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ListRequests_lists_requests_filtered_by_state_and_item()
    {
        var service = new ListRequestsService();
        var request = A.ListRequestsRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert lists_requests_filtered_by_state_and_item");
    }
}
