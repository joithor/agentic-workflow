using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;
using static Aw.Sindri.Tests.Workflow.Engine.LoopFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// '{ loop, below: cap }' (spec §3.5, §3.6, §3.7, #1009): a guard that is false at the cap makes its transition not match, so the first later
/// transition whose 'when' holds is taken; the loop parks with rounds only when the transition taken enters the spent loop.
/// </summary>
public sealed class BelowCapFallThroughTests
{
    private const int Guarded = 2;
    private const int Fallback = 5;

    private static readonly WhenClause _belowCap = new(Outcome: null, Fact: null, Is: null, Revisions, LoopBound.Cap, All: null, Any: null, Not: null);

    /// <summary>The step recipe, with 'checks -> produce' on 'failed' guarded below the revisions cap and a labelled 'checks -> done' on 'failed' after it.</summary>
    private static Recipe WithFallback(bool guarded)
    {
        var failed = new WhenClause(OutcomeName.From("failed"), Fact: null, Is: null, Loop: null, Below: null, All: null, Any: null, Not: null);
        var transitions = StepRecipe.Transitions.ToList();
        transitions[Guarded] = transitions[Guarded] with
        {
            When = guarded ? new WhenClause(Outcome: null, Fact: null, Is: null, Loop: null, Below: null, All: [failed, _belowCap], Any: null, Not: null) : failed,
            Produces = [],
        };
        transitions.Add(new RecipeTransition(["checks"], "done", "step.completed", failed, [], [], [], [], Label: "the revisions are spent: finish"));
        return StepRecipe with { Transitions = transitions };
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(2, Guarded)]
    [InlineData(3, Fallback)]
    public void A_false_below_cap_guard_does_not_match_and_the_next_transition_with_the_outcome_is_taken(int round, int expected)
    {
        var checks = In("checks", new LoopState(round, 3, Last: null));

        var match = TransitionDecision.Match(checks, WithFallback(guarded: true), ChecksFailedTrigger().Trigger);

        Assert.Equal(new TransitionMatch(expected, MatchMiss.None), match);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_false_below_cap_guard_applies_the_fallback_transition_and_parks_nothing()
    {
        var checks = In("checks", new LoopState(3, 3, Last: null));

        var decision = Decide(checks, WithFallback(guarded: true), ChecksFailedTrigger().Trigger, rules: EngineRules.Default);

        var after = Assert.IsType<Applied>(decision).Changes[0].After;
        Assert.Equal("done", after.State);
        Assert.Null(after.Park);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_loop_cap_still_parks_with_rounds_when_the_transition_taken_enters_the_spent_loop()
    {
        var checks = In("checks", new LoopState(3, 3, Last: null));

        var decision = Decide(checks, WithFallback(guarded: false), ChecksFailedTrigger().Trigger, rules: EngineRules.Default);

        var after = Assert.IsType<Applied>(decision).Changes[0].After;
        Assert.Equal("checks", after.State);
        Assert.Equal(ParkReason.Rounds, after.Park);
    }
}
