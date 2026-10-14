using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.CreateItem;

public sealed class CreateItemTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task CreateItem_appends_item_created_with_a_new_item_id()
    {
        var service = new CreateItemService();
        var request = A.CreateItemRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert appends_item_created_with_a_new_item_id");
    }
}
