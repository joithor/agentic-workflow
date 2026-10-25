using Aw.Judge.Core.Errors;
using Aw.Judge.Features.SetLabel;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.SetLabel;

// 'judge label set <itemId> <label|skip>': a manual override for one item.
// Red until cutover unit 6.3: the stub takes no ports yet, so the lander passes the fakes it builds in.
public sealed class SetLabelTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task SetLabel_refuses_an_item_that_does_not_exist_with_AW_JUDGE_013()
    {
        var result = await new SetLabelService().Handle(A.SetLabelRequest().Item("no-such-item").Build(), _ct);

        Assert.True(result.IsError);
        Assert.Equal(JudgeErrors.UnknownItem.Code, result.Error.Code);
    }
}
