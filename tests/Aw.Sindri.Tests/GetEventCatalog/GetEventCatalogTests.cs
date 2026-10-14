using Aw.Sindri.Features.GetEventCatalog;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetEventCatalog;

public sealed class GetEventCatalogTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetEventCatalog_lists_each_public_type_with_the_views_it_invalidates()
    {
        var service = new GetEventCatalogService();
        var request = A.GetEventCatalogRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert lists_each_public_type_with_the_views_it_invalidates");
    }
}
