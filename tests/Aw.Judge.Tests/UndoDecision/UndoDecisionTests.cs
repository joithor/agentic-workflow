using Aw.Judge.Features.UndoDecision;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.UndoDecision;

public sealed class UndoDecisionTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task UndoDecision_marks_the_decision_undone_at_the_clock_time()
    {
        var service = new UndoDecisionService();
        var request = A.UndoDecisionRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert marks_the_decision_undone_at_the_clock_time");
    }
}
