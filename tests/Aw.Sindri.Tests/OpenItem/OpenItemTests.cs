using Aw.Sindri.Features.OpenItem;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OpenItem;

public sealed class OpenItemTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task OpenItem_parks_when_a_launch_is_in_flight()
    {
        var service = new OpenItemService();
        var request = A.OpenItemRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert parks_when_a_launch_is_in_flight");
    }
}
