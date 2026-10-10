using Aw.Judge.Core.Errors;
using Aw.Judge.Features.ApproveDecision;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.ApproveDecision;

public sealed class ApproveDecisionTests
{
    private readonly InMemoryDecisionLog _decisions = new();

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveDecision_prefixes_the_reason_code_with_approved()
    {
        _ = await _decisions.Record(A.Decision().Build(), CancellationToken.None);

        var result = await new ApproveDecisionService(_decisions).Handle(A.ApproveDecisionRequest().Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("approved:model", result.Value.Decision.ReasonCode);
        Assert.Equal("approved:model", Assert.Single(_decisions.Decisions).ReasonCode);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveDecision_returns_UnknownDecision_for_an_unrecorded_id()
    {
        var result = await new ApproveDecisionService(_decisions).Handle(A.ApproveDecisionRequest().Build(), CancellationToken.None);

        Assert.Equal(JudgeErrors.UnknownDecision.Code, result.Error.Code);
    }
}
