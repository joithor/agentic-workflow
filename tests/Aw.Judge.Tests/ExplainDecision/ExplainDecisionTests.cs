using Aw.Judge.Features.ExplainDecision;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.ExplainDecision;

public sealed class ExplainDecisionTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ExplainDecision_returns_the_recorded_decision()
    {
        var service = new ExplainDecisionService();
        var request = A.ExplainDecisionRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert returns_the_recorded_decision");
    }
}
