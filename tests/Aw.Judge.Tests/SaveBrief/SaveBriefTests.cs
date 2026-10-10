using Aw.Judge.Features.SaveBrief;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.SaveBrief;

public sealed class SaveBriefTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task SaveBrief_saves_the_brief_unmapped_at_the_clock_time()
    {
        var service = new SaveBriefService();
        var request = A.SaveBriefRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert saves_the_brief_unmapped_at_the_clock_time");
    }
}
