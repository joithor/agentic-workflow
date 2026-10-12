using Aw.Sindri.Features.RebuildViews;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.RebuildViews;

public sealed class RebuildViewsTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task RebuildViews_appends_views_rebuilt_and_asks_the_projector_to_rebuild()
    {
        var service = new RebuildViewsService();
        var request = A.RebuildViewsRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert appends_views_rebuilt_and_asks_the_projector_to_rebuild");
    }
}
