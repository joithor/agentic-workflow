using Aw.Judge.Features.LabelOutcomes;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.LabelOutcomes;

// 'judge label outcomes': an 'ask-check' item is labelled from the next real prompt in the session's transcript.
// Red until cutover unit 6.3: the stub takes no ports yet, so the lander passes the fakes it builds in.
public sealed class LabelOutcomesTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task LabelOutcomes_labels_nothing_when_there_are_no_ask_check_items()
    {
        var result = await new LabelOutcomesService().Handle(A.LabelOutcomesRequest().Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(new LabelOutcomesResponse(Labeled: 0, NoSignal: 0), result.Value);
    }
}
