using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.LoopFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// Loop rounds (spec §3.7, §3.8, §5.1, §5.5): a round is counted on the transition back into a looped state and on a
/// re-run after a refusal, never on a pop; past the last allowed round the loop parks with rounds (kind loop).
/// </summary>
public sealed class LoopRoundsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Reentering_the_looped_state_counts_the_next_round_and_keeps_the_mark()
    {
        var mark = Findings("test VersionFlag_prints_the_version fails: printed nothing");

        var verdict = LoopRules.Reenter(In("checks", new LoopState(1, 3, Last: null)), StepRecipe, "produce", mark);

        Assert.Null(verdict.Park);
        Assert.Equal(new LoopState(2, 3, mark), verdict.Next);
    }

    [Fact, Trait("Lane", "unit")]
    public void Workflow_step_rounds_case_parks_rounds_on_the_third_failure()
    {
        // rounds.jsonl: produce (round 1) -> checks failed -> produce (2) -> checks failed -> produce (3) -> checks failed -> parked: rounds
        var loop = new LoopState(1, 3, Last: null);
        foreach (var findings in new[] { "printed nothing", "printed 'unknown'" })
        {
            var verdict = LoopRules.Reenter(In("checks", loop), StepRecipe, "produce", Findings(findings));
            Assert.Null(verdict.Park);
            loop = verdict.Next!; // why: asserted no park just above, so the loop goes on
        }

        Assert.Equal(3, loop.Round);
        var third = LoopRules.Reenter(In("checks", loop), StepRecipe, "produce", Findings("printed '0.0.0'"));

        Assert.Null(third.Next);
        var park = Assert.IsType<Suspension>(third.Park);
        Assert.Equal(SuspensionKind.Park, park.Kind);
        Assert.Equal(ParkReason.Rounds, park.Reason);
        Assert.Equal(ParkKind.Loop, park.ParkKind);
        Assert.Equal(ResumeKind.Transition, park.Resume.Kind);
        Assert.Null(park.Resume.Withheld); // why: the decision stores the withheld transition it matched
        Assert.Equal("revisions used all 3 rounds", park.Question);
    }

    [Fact, Trait("Lane", "unit")]
    public void Reenter_on_a_state_without_a_loop_says_nothing()
    {
        var verdict = LoopRules.Reenter(In("produce", new LoopState(1, 3, Last: null)), StepRecipe, "checks", EmptyDiff);

        Assert.Equal(new LoopVerdict(Next: null, Park: null), verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public void At_cap_is_the_last_allowed_round()
    {
        Assert.False(LoopRules.AtCap(In("produce", new LoopState(1, 3, Last: null)), StepRecipe, "produce"));
        Assert.False(LoopRules.AtCap(In("produce", new LoopState(2, 3, Last: null)), StepRecipe, "produce"));
        Assert.True(LoopRules.AtCap(In("produce", new LoopState(3, 3, Last: null)), StepRecipe, "produce"));
        Assert.False(LoopRules.AtCap(In("produce", new LoopState(3, 5, Last: null)), StepRecipe, "produce"));
        Assert.False(LoopRules.AtCap(In("checks", new LoopState(3, 3, Last: null)), StepRecipe, "checks"));
    }

    [Fact, Trait("Lane", "unit")]
    public void At_cap_before_the_loop_has_counters_reads_round_one_of_the_cap()
    {
        var capOne = StepRecipe with
        {
            Loops = new Dictionary<string, LoopSpec>(StringComparer.Ordinal)
            {
                [Revisions] = new(1, LoopSpec.DefaultNoProgress),
            },
        };

        Assert.False(LoopRules.AtCap(In("produce"), StepRecipe, "produce"));
        Assert.True(LoopRules.AtCap(In("produce"), capOne, "produce"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Refused_step_reruns_count_rounds_up_to_the_cap()
    {
        // §5.5 step 3: below the cap the decision re-runs the Step, bumps the round and keeps the refusal's mark;
        // at the cap a refusal parks with rounds (kind refusal).
        var loop = new LoopState(1, 3, Last: null);
        var reasons = new[] { "test A fails", "test B fails" };
        foreach (var reason in reasons)
        {
            var instance = In("produce", loop);
            Assert.False(LoopRules.AtCap(instance, StepRecipe, "produce"));
            Assert.False(LoopRules.NoProgress(StepRecipe.Loops[Revisions], loop.Last, Failure(reason)));
            loop = loop with { Round = loop.Round + 1, Last = Failure(reason) };
        }

        Assert.True(LoopRules.AtCap(In("produce", loop), StepRecipe, "produce"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Popping_a_frame_never_counts_a_round()
    {
        var loop = new LoopState(2, 3, Findings("A"));
        var parked = Parked(In("produce", loop), ParkReason.NeedsAnswer, ParkKind.Step, ResumeKind.Rerun);

        var popped = Popped(parked);

        Assert.Equal(loop, popped.Loops[Revisions]);
        Assert.Equal("2", LoopRules.Values(parked, StepRecipe)["loop.revisions"]);
        Assert.Equal("2", LoopRules.Values(popped, StepRecipe)["loop.revisions"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Loop_values_are_the_round_and_the_last_allowed_round()
    {
        var values = LoopRules.Values(In("produce", new LoopState(2, 3, Last: null)), StepRecipe);

        Assert.Equal("2", values["loop.revisions"]);
        Assert.Equal("3", values["loop.revisions.cap"]);
        Assert.Equal(2, values.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void Loop_values_before_the_first_entry_are_round_zero_of_the_cap()
    {
        var values = LoopRules.Values(In("checks"), StepRecipe);

        Assert.Equal("0", values["loop.revisions"]);
        Assert.Equal("3", values["loop.revisions.cap"]);
    }
}
