using Aw.Judge.Features.ApproveDecision;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.ApproveDecision;

public sealed class ApproveDecisionTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ApproveDecision_prefixes_the_reason_code_with_approved()
    {
        var service = new ApproveDecisionService();
        var request = A.ApproveDecisionRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert prefixes_the_reason_code_with_approved");
    }
}
