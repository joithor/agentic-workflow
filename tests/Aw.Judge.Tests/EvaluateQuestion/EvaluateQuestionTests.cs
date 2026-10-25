using Aw.Judge.Core.Errors;
using Aw.Judge.Features.EvaluateQuestion;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.EvaluateQuestion;

// 'judge eval <question>': one provider scored against the labels, called directly.
// Red until cutover unit 6.3: the stub takes no ports yet, so the lander passes the fakes it builds in.
public sealed class EvaluateQuestionTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task EvaluateQuestion_refuses_a_variant_the_question_does_not_have()
    {
        var result = await new EvaluateQuestionService().Handle(A.EvaluateQuestionRequest().Variant("no-such-variant").Build(), _ct);

        Assert.True(result.IsError);
        Assert.Equal(JudgeErrors.Invalid.Code, result.Error.Code);
    }
}
