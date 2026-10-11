using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using static Aw.Sindri.Tests.Workflow.Engine.ChildFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>A child's row releasing its parent, answers forwarded to the child, and a parent's cancel cascading (spec §5.7).</summary>
public sealed class ChildReleaseTests
{
    private static readonly Deadline _rearmed = new(DeadlineId.For(Entry, DeadlineKind.State, 2), Later.AddDays(14), Remaining: null);

    private static AnswerGiven Answer => new(Terminal, Stamp('1'), AnswerVariant.Text, "use the cached build", Amount: null, Forwarded: null);

    [Fact, Trait("Lane", "unit")]
    public void Release_restores_the_wait_with_a_fresh_state_deadline()
    {
        foreach (var mirroring in new[] { MirroringPark, MirroringEscalation })
        {
            var released = ChildPropagation.Release(mirroring, Item, ChildId, Later);

            Assert.Empty(released.Suspended);
            Assert.Equal("running", released.State);
            Assert.Equal(Entry, released.Frame.Entry);
            Assert.Equal(ChildId, released.Frame.Child);
            Assert.Equal(Later, released.Frame.Since);
            Assert.Equal([_rearmed], released.Frame.Deadlines);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Release_with_no_mirror_returns_the_same_instance()
    {
        Assert.Same(Waiting, ChildPropagation.Release(Waiting, Item, ChildId, Later));
        Assert.Same(MirroringPark, ChildPropagation.Release(MirroringPark, Item, OtherChildId, Later));
    }

    [Fact, Trait("Lane", "unit")]
    public void Release_carries_the_takeover_idle_arm_of_the_mirror_frame()
    {
        var idle = new Deadline(DeadlineId.For(Entry, DeadlineKind.TakeoverIdle, 2), Due: null, Remaining: null);
        var mirroring = MirroringPark with { Frame = MirroringPark.Frame with { Deadlines = [idle] } };

        var released = ChildPropagation.Release(mirroring, Item, ChildId, Later);

        Assert.Equal([_rearmed, idle], released.Frame.Deadlines);
    }

    [Fact, Trait("Lane", "unit")]
    public void Release_under_a_takeover_keeps_the_takeover_and_pauses_the_fresh_deadline()
    {
        var taken = TakenOver(MirroringPark);

        var released = ChildPropagation.Release(taken, Item, ChildId, Later);

        Assert.Equal(EngineStates.TakenOver, released.State);
        var below = Assert.Single(released.Suspended);
        Assert.Equal(SuspensionKind.Takeover, below.Suspension.Kind);
        Assert.Equal("running", below.Frame.State);
        Assert.Equal(ChildId, below.Frame.Child);
        Assert.Equal([new Deadline(DeadlineId.For(Entry, DeadlineKind.State, 2), Due: null, TimeSpan.FromDays(14))], below.Frame.Deadlines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pending_park_on_the_mirrored_frame_takes_effect_instead_of_the_restore()
    {
        var timeout = new Suspension(SuspensionKind.Park, ParkReason.Timeout, ParkKind: null, Escalation: null, "running timed out", "service:workflow.clock", Mirrors: null);
        var rearm = new ResumeAction(ResumeKind.Rearm, Withheld: null, Gate: null, Effect: null);
        var mirroring = MirroringPark with { Suspended = [MirroringPark.Suspended[0] with { Pending = new PendingPark(timeout, rearm) }] };

        var released = ChildPropagation.Release(mirroring, Item, ChildId, Later);

        Assert.Equal(EngineStates.Parked, released.State);
        Assert.Null(released.Frame.Child);
        Assert.Equal(Later, released.Frame.Since);
        var below = Assert.Single(released.Suspended);
        Assert.Equal(timeout, below.Suspension);
        Assert.Equal(rearm, below.Resume);
        Assert.Null(below.Pending);
        Assert.All(below.Frame.Deadlines, d => Assert.Null(d.Due));
        Assert.Equal(ParkReason.Timeout, released.Park);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pending_park_mirroring_the_child_is_cleared()
    {
        var parked = Push(Waiting, new SuspendOutcome(
            new Suspension(SuspensionKind.Park, ParkReason.Timeout, ParkKind: null, Escalation: null, "running timed out", "service:workflow.clock", Mirrors: null),
            new ResumeAction(ResumeKind.Rearm, Withheld: null, Gate: null, Effect: null)));
        var mirror = MirroringPark.Suspended[0];
        var pending = parked with { Suspended = [parked.Suspended[0] with { Pending = new PendingPark(mirror.Suspension, mirror.Resume) }] };

        var released = ChildPropagation.Release(pending, Item, ChildId, Later);

        Assert.Equal(EngineStates.Parked, released.State);
        Assert.Null(Assert.Single(released.Suspended).Pending);
        Assert.Equal(ParkReason.Timeout, released.Park);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_answer_to_a_parent_mirroring_a_child_goes_to_the_child()
    {
        Assert.Equal(ChildId, ChildPropagation.ForwardTo(MirroringPark, Answer));
        Assert.Equal(ChildId, ChildPropagation.ForwardTo(MirroringEscalation, Answer));
    }

    [Fact, Trait("Lane", "unit")]
    public void Cancel_takeover_and_answers_to_an_unmirrored_parent_stay_with_the_parent()
    {
        Assert.Null(ChildPropagation.ForwardTo(Waiting, Answer));
        Assert.Null(ChildPropagation.ForwardTo(TakenOver(MirroringPark), Answer));
        Assert.Null(ChildPropagation.ForwardTo(MirroringPark, new CancelRequested(Terminal, CancelOrigin.Command, Stamp('1'), Sequence: null, Parent: null)));
        Assert.Null(ChildPropagation.ForwardTo(MirroringPark, new TakeoverRequested(Terminal, Stamp('1'))));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_forwarded_answer_is_the_child_trigger_on_behalf_of_the_parent()
    {
        var forwarded = ChildPropagation.Forward(MirroringPark, Answer, Stamp('2'));

        Assert.Equal(Terminal, forwarded.Actor);
        Assert.Equal(Stamp('2'), forwarded.Stamp);
        Assert.Equal(AnswerVariant.Text, forwarded.Variant);
        Assert.Equal("use the cached build", forwarded.Text);
        Assert.Equal(new OnBehalfOf(ParentId, Terminal), forwarded.Forwarded);
        Assert.Equal(Stamp('2').KeyPart(TriggerNames.Answer), forwarded.KeyPart);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_parent_cancel_cascades_to_every_child_still_open()
    {
        var drifted = Child(WorkflowInstanceId.From("WFI-" + new string('e', 32)), EngineStates.Parked);
        var children = new[]
        {
            Child(ChildId, "checks"),
            Child(OtherChildId, "done", outcome: OutcomeName.From("resolved")),
            drifted,
            Child(WorkflowInstanceId.From("WFI-" + new string('f', 32)), "checks", parent: WorkflowInstanceId.From("WFI-" + new string('b', 32))),
        };

        Assert.Equal([ChildId, drifted.Id], ChildPropagation.Cascade(Waiting, children));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cascaded_cancel_is_keyed_by_the_parent_and_its_version()
    {
        var cancel = ChildPropagation.CancelFor(Waiting, Inbox);

        Assert.Equal(Inbox, cancel.Actor);
        Assert.Equal(CancelOrigin.Parent, cancel.Origin);
        Assert.Null(cancel.Stamp);
        Assert.Equal(Waiting.Version, cancel.Sequence);
        Assert.Equal(ParentId, cancel.Parent);
        Assert.Equal($"cancel:parent:{ParentId.Value}:6", cancel.KeyPart);
        Assert.True(cancel.ExemptFromApproval);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_child_parks_eval_parks_and_resumes_the_parent_in_the_same_wait()
    {
        // recipes/sindri/evals/workflow.item/child-parks.jsonl from 'running': child.parked, child.resumed, then the
        // child's terminal row, applied the way the engine applies a ParentEffect.
        var park = ChildPropagation.ToParent(Waiting, Item, Changed(ChildChange.Parked, ParkReason.NoProgress, "checks failed twice"));
        var parked = Push(Waiting, Assert.IsType<SuspendOutcome>(park.Suspend));
        Assert.Equal((EngineStates.Parked, ParkReason.NoProgress), (parked.State, parked.Park));

        Assert.Equal(ParentEffectKind.Release, ChildPropagation.ToParent(parked, Item, Changed(ChildChange.Resumed)).Kind);
        var resumed = ChildPropagation.Release(parked, Item, ChildId, Later);
        Assert.Equal(("running", Entry, (WorkflowInstanceId?)ChildId), (resumed.State, resumed.Frame.Entry, resumed.Frame.Child));

        Assert.Same(resumed, ChildPropagation.Release(resumed, Item, ChildId, Later));
    }
}
