using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.LoopFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A launch that never started a session uses no loop round (spec §3.7, §5.5): the Step fails with reason 'launch', the failed transition re-enters the looped state,
/// and the round stays; the third launch failure in a row parks the instance with the last error, so an unbumped failure can't relaunch forever.
/// </summary>
public sealed class LaunchFailureRoundsTests
{
    private const string Error = "SND-LAUNCH-007: the launch activity failed: worktree add was cut off";

    [Fact, Trait("Lane", "unit")]
    public void Two_failed_launches_then_a_start_leave_the_round_at_one()
    {
        var instance = In("produce", new LoopState(1, 3, Last: null));

        instance = Applied(Decide(instance, LaunchFailed(instance)));
        instance = Applied(Decide(instance, LaunchFailed(instance)));

        Assert.Null(instance.Park);
        Assert.Equal((1, 2), (instance.Loops[Revisions].Round, instance.Loops[Revisions].LaunchFailures));
        Assert.Equal("1", LoopRules.Values(instance, StepRecipe)["loop.revisions"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_run_that_failed_after_it_started_counts_its_round_and_ends_the_streak()
    {
        var instance = In("produce", new LoopState(1, 3, Last: null));
        instance = Applied(Decide(instance, LaunchFailed(instance)));

        instance = Applied(Decide(instance, Failed(instance, "the agent gave up")));

        Assert.Equal((2, 0), (instance.Loops[Revisions].Round, instance.Loops[Revisions].LaunchFailures));
    }

    [Fact, Trait("Lane", "unit")]
    public void Three_launch_failures_in_a_row_park_unavailable_with_the_last_error()
    {
        var instance = In("produce", new LoopState(1, 3, Last: null));
        instance = Applied(Decide(instance, LaunchFailed(instance)));
        instance = Applied(Decide(instance, LaunchFailed(instance)));

        var third = Applied(Decide(instance, LaunchFailed(instance)));

        Assert.Equal(ParkReason.Unavailable, third.Park);
        Assert.Equal(1, third.Loops[Revisions].Round);
        Assert.Contains(Error, third.Suspension!.Question, StringComparison.Ordinal);
        Assert.Contains("unavailable after 2 retries", third.Suspension.Question, StringComparison.Ordinal);
        Assert.Equal(ResumeKind.Rerun, third.Suspension.Resume.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_history_from_before_the_unavailable_park_still_parks_needs_answer_on_the_third()
    {
        var instance = In("produce", new LoopState(1, 3, Last: null));
        instance = Applied(Decide(instance, LaunchFailed(instance)));
        instance = Applied(Decide(instance, LaunchFailed(instance)));

        var third = Applied(TransitionDecision.Decide(new DecisionInput(instance, Retrying, LaunchFailed(instance), GateAnswers.None, EngineFixture.Context() with { LegacyUnavailable = true })));

        Assert.Equal(ParkReason.NeedsAnswer, third.Park);
        Assert.Contains(Error, third.Suspension!.Question, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_history_from_before_the_fix_counts_a_round_for_a_failed_launch()
    {
        var instance = In("produce", new LoopState(1, 3, Last: null));

        var after = Applied(Decide(instance, LaunchFailed(instance), legacy: true));

        Assert.Equal(2, after.Loops[Revisions].Round);
    }

    private static Decision Decide(WorkflowInstance instance, Trigger trigger, bool legacy = false) =>
        TransitionDecision.Decide(new DecisionInput(instance, Retrying, trigger, GateAnswers.None, EngineFixture.Context() with { LegacyLaunchRound = legacy }));

    private static WorkflowInstance Applied(Decision decision) => Assert.IsType<Applied>(decision).Changes[0].After;

    private static StepFailed LaunchFailed(WorkflowInstance instance) => Failed(instance, StepFailed.LaunchReason) with { Detail = Error };

    // each failure of a run has its own report digest, as the workflow derives it from its run count
    private static StepFailed Failed(WorkflowInstance instance, string reason) =>
        new("service:agent:step.produce", instance.Frame.Run!.Token, reason, Tokens: 0, Digest(string.Create(CultureInfo.InvariantCulture, $"{reason}\n{instance.Frame.Entry}\n{instance.Loops[Revisions].LaunchFailures}")));

    private static string Digest(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>The 'workflow.step' shape with a 'step.failed' transition that re-enters 'produce', as the shipped agent recipes retry a failed Step.</summary>
    private static Recipe Retrying { get; } = StepRecipe with
    {
        Transitions = [.. StepRecipe.Transitions, new RecipeTransition(["produce"], "produce", "step.failed", When: null, [], [], [], [])],
    };
}
