using Aw.Judge.Core.Errors;
using Aw.Judge.Features.Adjudicate;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.Adjudicate;

// 'judge adjudicate <question>': a stronger model labels the unlabelled items, a label only on a 2-of-3 majority.
// Red until cutover unit 6.3: the stub takes no ports yet, so the lander passes the fakes it builds in.
public sealed class AdjudicateTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Adjudicate_refuses_a_question_the_judge_does_not_ask()
    {
        var result = await new AdjudicateService().Handle(A.AdjudicateRequest().Question("no-such-question").Build(), _ct);

        Assert.True(result.IsError);
        Assert.Equal(JudgeErrors.Invalid.Code, result.Error.Code);
    }
}
