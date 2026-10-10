using Aw.Judge.Features.SetBriefAgentId;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.SetBriefAgentId;

public sealed class SetBriefAgentIdTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task SetBriefAgentId_maps_the_brief_to_the_agent()
    {
        var service = new SetBriefAgentIdService();
        var request = A.SetBriefAgentIdRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert maps_the_brief_to_the_agent");
    }
}
