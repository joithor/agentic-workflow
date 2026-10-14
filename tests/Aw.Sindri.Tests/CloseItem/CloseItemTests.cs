using Aw.Sindri.Features.CloseItem;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.CloseItem;

public sealed class CloseItemTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task CloseItem_closes_the_item_at_the_version_the_human_saw()
    {
        var service = new CloseItemService();
        var request = A.CloseItemRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert closes_the_item_at_the_version_the_human_saw");
    }
}
