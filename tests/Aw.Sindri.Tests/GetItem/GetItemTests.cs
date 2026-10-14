using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetItem;

public sealed class GetItemTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetItem_shows_the_item_with_its_version_and_actions()
    {
        var service = new GetItemService();
        var request = A.GetItemRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert shows_the_item_with_its_version_and_actions");
    }
}
