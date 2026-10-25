using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.Layout;

/// <summary>F5 PR B: an open desk task's form carries the outcome the step before the stop recommends, read from 'fact:recommendation', and drops one the stop cannot take.</summary>
[Lane(TestLane.Unit)]
public sealed class DeskRecommendationTests
{
    private static readonly OutcomeName[] _outcomes = [OutcomeName.From("approve"), OutcomeName.From("revise"), OutcomeName.From("drop")];

    [Fact]
    public void A_recommendation_of_one_of_the_stops_outcomes_is_read_with_its_reason()
    {
        var recommended = Recommendations.Of("{\"outcome\":\"revise\",\"why\":\"Two questions are still open.\"}", _outcomes);

        Assert.Equal(("revise", "Two questions are still open."), (recommended?.Outcome, recommended?.Why));
    }

    [Fact]
    public void A_recommendation_without_a_reason_has_none()
    {
        Assert.Null(Recommendations.Of("{\"outcome\":\"approve\"}", _outcomes)?.Why);
        Assert.Null(Recommendations.Of("{\"outcome\":\"approve\",\"why\":\" \"}", _outcomes)?.Why);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[\"approve\"]")]
    [InlineData("{\"why\":\"no outcome\"}")]
    [InlineData("{\"outcome\":3}")]
    [InlineData("{\"outcome\":\"merge\",\"why\":\"not an outcome of this stop\"}")]
    public void A_missing_unreadable_or_foreign_recommendation_is_dropped_not_an_error(string? text) =>
        Assert.Null(Recommendations.Of(text, _outcomes));

    [Fact]
    public void The_form_of_an_open_desk_task_carries_the_recommendation_and_leaves_it_out_when_there_is_none()
    {
        var instance = StatusCases.Instance("desk-task");
        var withText = InstanceViews.Of(instance, StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext(Recommendation: "{\"outcome\":\"done\",\"why\":\"All checks passed.\"}"));
        var without = StatusCases.View("desk-task");

        Assert.Equal(("done", "All checks passed."), (withText.Desk?.Recommended?.Outcome, withText.Desk?.Recommended?.Why));
        Assert.Null(without.Desk?.Recommended);
    }

    [Fact]
    public void A_recommendation_the_open_steps_outcomes_do_not_include_is_not_on_the_form()
    {
        var instance = StatusCases.Instance("desk-task");

        var view = InstanceViews.Of(instance, StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext(Recommendation: "{\"outcome\":\"approve\"}"));

        Assert.NotNull(view.Desk);
        Assert.Null(view.Desk.Recommended);
    }
}
