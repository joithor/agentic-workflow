using Aw.Judge.Features.GetBrief;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.GetBrief;

public sealed class GetBriefTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetBrief_returns_the_brief_a_tool_call_saved()
    {
        var service = new GetBriefService();
        var request = A.GetBriefRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert returns_the_brief_a_tool_call_saved");
    }
}
