using Aw.Judge.Features.MapBriefByDispatch;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.MapBriefByDispatch;

public sealed class MapBriefByDispatchTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task MapBriefByDispatch_returns_the_oldest_unmapped_brief_of_the_dispatch()
    {
        var service = new MapBriefByDispatchService();
        var request = A.MapBriefByDispatchRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert returns_the_oldest_unmapped_brief_of_the_dispatch");
    }
}
