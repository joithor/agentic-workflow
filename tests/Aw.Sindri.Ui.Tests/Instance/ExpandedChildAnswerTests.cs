using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The expanded state of an instance whose answer a child decides: a line "Answer in the child" that follows the served child when there is one, else asks for the Children panel.</summary>
[Lane(TestLane.Unit)]
public sealed class ExpandedChildAnswerTests
{
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private ExpandedStateViewModel Of(StatusFields status) =>
        new(new CurrentState(status.Header.State, Step: null, Gates: [], Desk: null, status), status.Header.Address, _clipboard, _clock);

    [Theory]
    [InlineData("escalated-child")]
    [InlineData("mirrored-budget")]
    [InlineData("mirrored-budget-no-child")]
    public void A_pointer_shows_the_line_and_no_answer_command(string scenario)
    {
        var status = FixtureInstances.Status(scenario);

        var expanded = Of(status);

        Assert.True(expanded.AnswersInChild);
        Assert.Equal(Labels.AnswerInChild, expanded.AnswerInChildText);
        Assert.False(expanded.HasAnswer);
        Assert.DoesNotContain(expanded.Commands, c => c.Protected is not null || c.Cli.Contains(status.Answer!.Cli, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("escalated-child")]
    [InlineData("mirrored-budget")]
    public void With_a_served_child_its_name_is_the_child_it_opens(string scenario)
    {
        var status = FixtureInstances.Status(scenario);

        var expanded = Of(status);

        Assert.Equal(Labels.AnswerInChildName(status.Child!.Address), expanded.AnswerInChildName);
    }

    [Fact]
    public void Without_a_child_its_name_says_it_moves_to_the_children_panel()
    {
        var expanded = Of(FixtureInstances.Status("mirrored-budget-no-child"));

        Assert.Equal(Labels.AnswerInChildPanelName, expanded.AnswerInChildName);
    }

    [Theory]
    [InlineData("escalated-child")]
    [InlineData("mirrored-budget")]
    public void With_a_served_child_it_opens_that_child_by_its_id(string scenario)
    {
        var status = FixtureInstances.Status(scenario);
        var expanded = Of(status);
        var opened = new List<FrameOpenEventArgs>();
        expanded.ChildRequested += (_, e) => opened.Add(e);

        expanded.AnswerInTheChild();

        var request = Assert.Single(opened);
        Assert.Equal((status.Child!.Address, status.Child.Id), (request.Address, request.Id));
        Assert.Equal(status.Answer!.Child, request.Address);
    }

    [Fact]
    public void Without_a_child_it_asks_for_the_children_panel_and_opens_nothing()
    {
        var expanded = Of(FixtureInstances.Status("mirrored-budget-no-child"));
        var opened = new List<FrameOpenEventArgs>();
        var panel = 0;
        expanded.ChildRequested += (_, e) => opened.Add(e);
        expanded.ChildrenRequested += (_, _) => panel++;

        expanded.AnswerInTheChild();

        Assert.Empty(opened);
        Assert.Equal(1, panel);
    }

    [Fact]
    public void A_served_pointer_that_is_not_the_answers_child_is_never_guessed()
    {
        var status = FixtureInstances.Status("escalated-child");
        var other = status with { Child = status.Child! with { Address = status.Child.Address + "/other" } };
        var expanded = Of(other);
        var opened = new List<FrameOpenEventArgs>();
        var panel = 0;
        expanded.ChildRequested += (_, e) => opened.Add(e);
        expanded.ChildrenRequested += (_, _) => panel++;

        Assert.Equal(Labels.AnswerInChildPanelName, expanded.AnswerInChildName);   // a child is served, but not the answer's: the name falls back with the click
        expanded.AnswerInTheChild();

        Assert.Empty(opened);
        Assert.Equal(1, panel);
    }

    [Fact]
    public void A_state_that_does_not_point_at_a_child_has_no_such_line()
    {
        var status = FixtureInstances.Status("taken-over");

        var expanded = Of(status);

        Assert.False(expanded.AnswersInChild);
    }
}
