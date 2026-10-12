using Aw.Kernel.Testing;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>What 'GetInstance' renders into the view (engine spec §3.8, §6.4): detail, 'next', answer lines, commands, budget.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceViewsTests
{
    [Fact]
    public void A_desk_task_fills_the_recipe_next_and_names_the_complete_line_with_its_required_output()
    {
        var view = StatusCases.View("desk-task");

        Assert.Equal(("produce", "desk task"), (view.State, view.Detail));
        Assert.Equal(
            "Produce the Step output for T-3 (round 1/3), then run:\nsindri workflow complete T-3/step/1 --run 1.1 --outcome done --output fact:output=<file>",
            view.Next);
        Assert.Equal("complete, takeover, cancel", StatusLayout.Commands(view));
        Assert.Equal((0, 200_000, 0, 400_000), (view.Budget!.State, view.Budget.StateCap, view.Budget.Instance, view.Budget.InstanceCap));
        Assert.Null(view.Answer);
    }

    [Theory]
    [InlineData("parked-rounds", "rounds", "sindri workflow answer T-3/step/1 --grant 1     (at a terminal)")]
    [InlineData("parked-no-progress-child", "no-progress (child)", "sindri workflow answer T-3/step/1 --grant 1     (at a terminal; forwarded to the child)")]
    [InlineData("parked-budget", "budget", "sindri workflow answer T-3/step/1 --grant-tokens 100k     (at a terminal; once per park)")]
    [InlineData("parked-needs-answer-step", "needs-answer", "sindri workflow answer T-3/step/1 --text <answer>")]
    [InlineData("parked-needs-answer-wait", "needs-answer", "sindri workflow answer T-3/step/1 --resume [--text <answer>]")]
    [InlineData("parked-timeout", "timeout", "sindri workflow answer T-3/step/1 --resume")]
    [InlineData("parked-no-runtime", "SND-WF-010", "sindri workflow answer T-3/step/1 --resume")]
    [InlineData("escalated-gate", "gate", "sindri workflow answer T-3/step/1 --accept     (at a terminal)")]
    public void Each_park_reason_has_its_detail_and_answer_line(string name, string detail, string answer)
    {
        var view = StatusCases.View(name);

        Assert.Equal((detail, answer), (view.Detail, view.Answer));
        Assert.StartsWith("answer", StatusLayout.Commands(view), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("parked-revoked", "spec-conflict (revoked)", "cancel")]
    [InlineData("parked-drift", "spec-conflict (drift)", "approve")]
    public void A_spec_conflict_park_takes_no_answer_and_names_its_way_out(string name, string detail, string way)
    {
        var view = StatusCases.View(name);

        Assert.Equal(detail, view.Detail);
        Assert.Equal("takeover, cancel", StatusLayout.Commands(view));
        Assert.Contains(way, view.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public void A_drift_behind_a_park_names_the_block_and_the_reapproval_first()
    {
        var view = StatusCases.View("parked-rounds-with-drift");

        Assert.Equal(
            "step.produce changed since workflow.step@" + Hash12() + " was approved; re-approve it before you answer: sindri recipe approve workflow.step@" + Hash12(),
            view.Drift);
    }

    [Fact]
    public void An_ended_instance_has_its_outcome_as_detail_and_nothing_else()
    {
        var view = StatusCases.View("done");

        Assert.Equal(("done", "resolved", string.Empty), (view.State, view.Detail, view.Next));
        Assert.Empty(view.AllowedCommands);
        Assert.Null(view.Budget);
        Assert.Null(view.Answer);
    }

    private static string Hash12() => StatusCases.Recipe.Version.Hash.Value["sha256:".Length..][..12];
}
