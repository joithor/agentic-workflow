using Aw.Judge.Features.GetLabelStatus;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.GetLabelStatus;

// 'judge label status': coverage and label agreement per question.
// Red until cutover unit 6.3: the stub takes no ports yet, so the lander passes the fakes it builds in.
public sealed class GetLabelStatusTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task GetLabelStatus_lists_no_questions_when_no_item_was_imported()
    {
        var result = await new GetLabelStatusService().Handle(A.GetLabelStatusRequest().Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Questions);
    }
}
