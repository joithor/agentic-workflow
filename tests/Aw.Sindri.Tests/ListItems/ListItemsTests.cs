using Aw.Sindri.Features.ListItems;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ListItems;

public sealed class ListItemsTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ListItems_lists_items_filtered_by_state_and_repo()
    {
        var service = new ListItemsService();
        var request = A.ListItemsRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert lists_items_filtered_by_state_and_repo");
    }
}
