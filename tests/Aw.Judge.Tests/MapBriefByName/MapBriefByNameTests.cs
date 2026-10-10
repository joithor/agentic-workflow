using Aw.Judge.Features.MapBriefByName;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.MapBriefByName;

public sealed class MapBriefByNameTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task MapBriefByName_returns_the_oldest_unmapped_brief()
    {
        var service = new MapBriefByNameService();
        var request = A.MapBriefByNameRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert returns_the_oldest_unmapped_brief");
    }
}
