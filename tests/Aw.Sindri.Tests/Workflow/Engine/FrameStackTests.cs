using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;
using static Aw.Sindri.Tests.Workflow.Engine.FrameFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>Entering states, and pushing and popping engine-owned frames (spec §5.1, W16, W19, §3.8).</summary>
public sealed class FrameStackTests
{
    [Fact, Trait("Lane", "unit")]
    public void Enter_opens_a_new_entry_with_its_state_deadline_and_run()
    {
        var entered = FrameRules.Enter(Producing(), Recipe, "produce", Context(T0.AddHours(1)));

        Assert.Equal("produce", entered.State);
        Assert.Equal(4, entered.Frame.Entry);
        Assert.Equal(new OpenRun(RunToken.For(4, 1), StepRuntime.Agent, Launch: null, Task: null), entered.Frame.Run);
        Assert.Equal(new Deadline(DeadlineId.For(4, DeadlineKind.State, 1), T0.AddHours(5), Remaining: null), Assert.Single(entered.Frame.Deadlines));
        Assert.Equal(T0.AddHours(1), entered.Frame.Since);
        Assert.Null(entered.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void Enter_runs_a_desk_capable_step_on_the_desk_with_at_least_the_desk_timeout()
    {
        var entered = FrameRules.Enter(Producing(), Recipe, "checks", Context(T0));

        Assert.Equal(StepRuntime.Desk, entered.Frame.Run?.Runtime);
        Assert.Equal(RunToken.For(4, 1), entered.Frame.Run?.Token);
        Assert.Equal(T0.AddDays(2), Assert.Single(entered.Frame.Deadlines).Due);
    }

    [Fact, Trait("Lane", "unit")]
    public void Enter_a_terminal_state_sets_the_outcome_and_arms_nothing()
    {
        var entered = FrameRules.Enter(Producing(), Recipe, "done", Context(T0));

        Assert.Equal(OutcomeName.From("resolved"), entered.Outcome);
        Assert.Empty(entered.Frame.Deadlines);
        Assert.Null(entered.Frame.Run);
    }

    [Fact, Trait("Lane", "unit")]
    public void Park_clears_the_deadlines_and_keeps_entry_run_and_child()
    {
        var parked = Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm));

        Assert.Equal(EngineStates.Parked, parked.State);
        Assert.Equal(ParkReason.Timeout, parked.Park);
        Assert.Equal(3, parked.Frame.Entry);
        var below = Assert.Single(parked.Suspended).Frame;
        Assert.Equal(RunToken.For(3, 1), below.Run?.Token);
        Assert.All(below.Deadlines, d => Assert.Null(d.Due));
        Assert.All(below.Deadlines, d => Assert.Null(d.Remaining));
    }

    [Fact, Trait("Lane", "unit")]
    public void Takeover_pauses_the_deadlines_and_arms_takeover_idle()
    {
        var taken = FrameRules.TakeOver(Producing(), "human:cli", Context(T0.AddHours(1))).Value;

        Assert.Equal(EngineStates.TakenOver, taken.State);
        var paused = Assert.Single(Assert.Single(taken.Suspended).Frame.Deadlines);
        Assert.True(paused.Paused);
        Assert.Equal(TimeSpan.FromHours(3), paused.Remaining);
        var idle = Assert.Single(taken.Frame.Deadlines);
        Assert.Equal(DeadlineId.For(3, DeadlineKind.TakeoverIdle, 1), idle.Id);
        Assert.Equal(T0.AddHours(3), idle.Due);
    }

    [Fact, Trait("Lane", "unit")]
    public void Takeover_of_a_parked_instance_returns_to_the_park()
    {
        var parked = Suspended(Park(ParkReason.NeedsAnswer, ParkKind.Step, ResumeKind.Rerun));

        var taken = FrameRules.TakeOver(parked, "human:cli", Context(T0.AddHours(1))).Value;
        Assert.Equal(2, taken.Suspended.Count);
        var back = FrameRules.HandBack(taken, T0.AddHours(2)).Value;

        Assert.Equal(ResumeKind.Return, back.Resume.Kind);
        Assert.Equal(EngineStates.Parked, back.Instance.State);
        Assert.Equal(ParkReason.NeedsAnswer, back.Instance.Park);
        Assert.Equal(parked.Suspended, back.Instance.Suspended);
    }

    [Fact, Trait("Lane", "unit")]
    public void Takeover_of_a_taken_over_or_ended_instance_is_refused()
    {
        var taken = FrameRules.TakeOver(Producing(), "human:cli", Context(T0)).Value;

        Assert.Equal("SND-WF-003", FrameRules.TakeOver(taken, "human:cli", Context(T0)).Error.Code);
        Assert.Equal("SND-WF-003", FrameRules.TakeOver(FrameRules.Cancel(Producing(), T0), "human:cli", Context(T0)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Handback_without_a_takeover_is_refused()
    {
        var parked = Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm));

        var refused = FrameRules.HandBack(parked, T0);

        Assert.Equal("SND-WF-003", refused.Error.Code);
        Assert.Contains("(fix: ", refused.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Pop_keeps_entry_run_child_and_inbox()
    {
        var waiting = Instance(new Frame("await", 7, Run: null, ChildId, [new Deadline(DeadlineId.For(7, DeadlineKind.State, 1), T0.AddHours(2), Remaining: null)], T0));
        var parked = Suspended(Park(ParkReason.NeedsAnswer, ParkKind.Wait, ResumeKind.Reevaluate, CiFinished()), waiting);

        var popped = FrameRules.Pop(parked, Recipe, child: null, T0.AddHours(1)).Value;

        Assert.Equal("await", popped.Instance.State);
        Assert.Equal(7, popped.Instance.Frame.Entry);
        Assert.Equal(ChildId, popped.Instance.Frame.Child);
        Assert.Empty(popped.Instance.Suspended);
        Assert.Equal(ResumeKind.Reevaluate, popped.Resume.Kind);
        Assert.Equal(parked.Loops, popped.Instance.Loops);
    }

    [Fact, Trait("Lane", "unit")]
    public void Pop_keeps_the_open_run_token()
    {
        var parked = Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm));

        var popped = FrameRules.Pop(parked, Recipe, child: null, T0.AddHours(1)).Value;

        Assert.Equal(Producing().Frame.Run, popped.Instance.Frame.Run);
        Assert.Equal(T0.AddHours(1), popped.Instance.Frame.Since);
    }

    [Fact, Trait("Lane", "unit")]
    public void Rearmed_deadline_has_a_new_arm()
    {
        var parked = Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm));
        var answered = FrameRules.Pop(parked, Recipe, child: null, T0.AddHours(6)).Value.Instance;
        var taken = FrameRules.TakeOver(answered, "human:cli", Context(T0.AddHours(7))).Value;
        var back = FrameRules.HandBack(taken, T0.AddHours(9)).Value.Instance;

        var fresh = Assert.Single(answered.Frame.Deadlines, d => d.Id.Kind == DeadlineKind.State);
        Assert.Equal(DeadlineId.For(3, DeadlineKind.State, 2), fresh.Id);
        Assert.Equal(T0.AddHours(10), fresh.Due);
        var resumed = Assert.Single(back.Frame.Deadlines, d => d.Id.Kind == DeadlineKind.State);
        Assert.Equal(DeadlineId.For(3, DeadlineKind.State, 3), resumed.Id);
        Assert.Equal(T0.AddHours(12), resumed.Due);
    }

    [Fact, Trait("Lane", "unit")]
    public void Second_takeover_in_an_entry_gets_a_new_idle_arm()
    {
        var once = FrameRules.HandBack(FrameRules.TakeOver(Producing(), "human:cli", Context(T0)).Value, T0.AddMinutes(5)).Value.Instance;

        var twice = FrameRules.TakeOver(once, "human:cli", Context(T0.AddMinutes(10))).Value;

        Assert.Equal(DeadlineId.For(3, DeadlineKind.TakeoverIdle, 2), Assert.Single(twice.Frame.Deadlines).Id);
    }

    [Fact, Trait("Lane", "unit")]
    public void Park_on_a_parked_instance_waits_as_pending()
    {
        var parked = Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm));
        var drift = Park(ParkReason.SpecConflict, ParkKind.Drift, ResumeKind.Rearm);

        var pending = FrameRules.Suspend(parked, drift, T0.AddHours(1));
        var popped = FrameRules.Pop(pending, Recipe, child: null, T0.AddHours(2)).Value;

        Assert.Equal(ParkReason.Timeout, pending.Park);
        _ = Assert.Single(pending.Suspended);
        Assert.Equal(new PendingPark(drift.Suspension, drift.Resume), pending.Suspended[^1].Pending);
        Assert.Equal(new PendingPark(drift.Suspension, drift.Resume), popped.Pending);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_park_on_a_frame_whose_pending_park_is_taken_leaves_the_first_one_standing()
    {
        var drift = Park(ParkReason.SpecConflict, ParkKind.Drift, ResumeKind.Rearm);
        var pending = FrameRules.Suspend(Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm)), drift, T0.AddHours(1));

        var again = FrameRules.Suspend(pending, Park(ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun), T0.AddHours(2));

        // §5.1: a pending park already there stands and takes effect first; the slot holds one, so the later park changes nothing.
        Assert.Same(pending, again);
        Assert.Equal(new PendingPark(drift.Suspension, drift.Resume), again.Suspended[^1].Pending);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parks_on_a_parked_and_taken_over_instance_wait_one_per_suspended_frame()
    {
        var taken = FrameRules.TakeOver(Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm)), "human:cli", Context(T0.AddHours(1))).Value;
        var budget = Park(ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun);
        var drift = Park(ParkReason.SpecConflict, ParkKind.Drift, ResumeKind.Rearm);

        var first = FrameRules.Suspend(taken, budget, T0.AddHours(2));
        var second = FrameRules.Suspend(first, drift, T0.AddHours(3));
        var third = FrameRules.Suspend(second, Park(ParkReason.NeedsAnswer, ParkKind.Step, ResumeKind.Rerun), T0.AddHours(4));

        Assert.Equal(2, taken.Suspended.Count);
        Assert.Equal(new PendingPark(budget.Suspension, budget.Resume), first.Suspended[1].Pending);
        Assert.Null(first.Suspended[0].Pending);
        Assert.Equal(new PendingPark(drift.Suspension, drift.Resume), second.Suspended[0].Pending);
        Assert.Same(second, third);
    }

    [Fact, Trait("Lane", "unit")]
    public void Park_on_a_taken_over_instance_takes_effect_on_handback()
    {
        var taken = FrameRules.TakeOver(Producing(), "human:cli", Context(T0)).Value;
        var budget = Park(ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun);

        var pending = FrameRules.Suspend(taken, budget, T0.AddMinutes(1));
        var back = FrameRules.HandBack(pending, T0.AddMinutes(2)).Value;

        Assert.Equal(EngineStates.TakenOver, pending.State);
        Assert.Equal(budget.Suspension, back.Pending?.Suspension);
    }

    [Fact, Trait("Lane", "unit")]
    public void Revoke_park_replaces_the_stack_and_ends_a_takeover()
    {
        var parked = Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm));
        var taken = FrameRules.TakeOver(parked, "human:cli", Context(T0.AddHours(1))).Value;

        var revoked = FrameRules.Suspend(taken, Park(ParkReason.SpecConflict, ParkKind.Revoked, ResumeKind.None), T0.AddHours(2));

        Assert.Equal(EngineStates.Parked, revoked.State);
        var only = Assert.Single(revoked.Suspended);
        Assert.Equal(ParkKind.Revoked, only.Suspension.ParkKind);
        Assert.Equal("produce", only.Frame.State);
        Assert.Equal(RunToken.For(3, 1), only.Frame.Run?.Token);
    }

    [Fact, Trait("Lane", "unit")]
    public void Drift_park_on_a_drift_park_is_absorbed()
    {
        var drifted = Suspended(Park(ParkReason.SpecConflict, ParkKind.Drift, ResumeKind.Rearm));

        var again = FrameRules.Suspend(drifted, Park(ParkReason.SpecConflict, ParkKind.Drift, ResumeKind.Rearm), T0.AddHours(1));

        Assert.Same(drifted, again);
    }

    [Fact, Trait("Lane", "unit")]
    public void Mirrored_frame_pops_by_its_child_not_from_the_top()
    {
        var waiting = Instance(new Frame("await", 7, Run: null, ChildId, [], T0));
        var mirrored = Suspended(MirrorChild(ParkReason.NoProgress), waiting);

        Assert.Equal("SND-WF-003", FrameRules.Pop(mirrored, Recipe, child: null, T0).Error.Code);
        var popped = FrameRules.Pop(mirrored, Recipe, ChildId, T0).Value;
        Assert.Equal("await", popped.Instance.State);
        Assert.Equal(ChildId, popped.Instance.Frame.Child);
    }

    [Fact, Trait("Lane", "unit")]
    public void Cancel_drops_the_stack_and_ends_cancelled()
    {
        var taken = FrameRules.TakeOver(Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm)), "human:cli", Context(T0)).Value;

        var cancelled = FrameRules.Cancel(taken, T0.AddHours(1));

        Assert.Equal(EngineStates.Cancelled, cancelled.State);
        Assert.Equal(OutcomeName.From("cancelled"), cancelled.Outcome);
        Assert.Empty(cancelled.Suspended);
        Assert.Empty(cancelled.Frame.Deadlines);
        Assert.True(cancelled.Ended);
    }
}
