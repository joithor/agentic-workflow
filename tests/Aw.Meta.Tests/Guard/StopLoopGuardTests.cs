using Aw.Meta.Core.Guard;
using Aw.Meta.Core.Obligations;

namespace Aw.Meta.Tests.Guard;

public sealed class StopLoopGuardTests
{
    private static GuardDecision Step(GuardState state, string fingerprint, bool hookActive = true) =>
        StopLoopGuard.Decide(state, fingerprint, hookActive, max: 3);

    [Fact, Trait("Lane", "unit")]
    public void An_identical_set_blocks_max_times_and_then_a_continuation_is_allowed()
    {
        var state = GuardState.None;
        var blocks = new List<bool>();
        for (var i = 0; i < 5; i++)
        {
            var decision = Step(state, "Reply:m1");
            blocks.Add(decision.Block);
            state = decision.State;
        }

        Assert.Equal([true, true, true, false, false], blocks);
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_the_first_allowance_is_flagged_for_an_incident()
    {
        var third = Step(new GuardState("Reply:m1", 2), "Reply:m1");
        var fourth = Step(third.State, "Reply:m1");
        var fifth = Step(fourth.State, "Reply:m1");

        Assert.True(third.Block);
        Assert.Equal((false, true, true), (fourth.Block, fourth.Allowed, fourth.FirstAllowed));
        Assert.Equal((false, true, false), (fifth.Block, fifth.Allowed, fifth.FirstAllowed));
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_hook_active_it_keeps_blocking()
    {
        var decision = Step(new GuardState("Reply:m1", 9), "Reply:m1", hookActive: false);

        Assert.True(decision.Block);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_different_set_starts_counting_again_and_an_empty_set_resets()
    {
        var changed = Step(new GuardState("Reply:m1", 9), "Reply:m2");
        var cleared = Step(changed.State, string.Empty);

        Assert.Equal((true, 1), (changed.Block, changed.State.Count));
        Assert.Equal((false, GuardState.None), (cleared.Block, cleared.State));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_fingerprint_ignores_order()
    {
        Obligation[] one = [new(ObligationKind.Reply, "b", "s", "n"), new(ObligationKind.Reply, "a", "s", "n")];
        Obligation[] other = [one[1], one[0]];

        Assert.Equal(StopLoopGuard.Fingerprint(one), StopLoopGuard.Fingerprint(other));
        Assert.Equal(string.Empty, StopLoopGuard.Fingerprint([]));
    }
}
