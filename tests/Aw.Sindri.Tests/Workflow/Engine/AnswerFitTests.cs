using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using static Aw.Sindri.Tests.Workflow.Engine.FrameFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>Every answer does something (spec §3.8, §5.8, §6.4): which answer fits each suspension, and the resume action it runs.</summary>
public sealed class AnswerFitTests
{
    [Fact, Trait("Lane", "unit")]
    public void Answer_rounds_runs_transition()
    {
        var parked = Suspended(Park(ParkReason.Rounds, ParkKind.Loop, ResumeKind.Transition, CheckFailed()));

        Assert.Equal(ResumeKind.Transition, FrameRules.Fits(parked, Recipe, Answer(AnswerVariant.Grant, amount: 1)).Value.Kind);
        Refused(parked, Answer(AnswerVariant.Text, "try again"), "--grant 1");
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_no_progress_runs_transition()
    {
        var parked = Suspended(Park(ParkReason.NoProgress, ParkKind.Loop, ResumeKind.Transition, CheckFailed()));

        Assert.Equal(ResumeKind.Transition, FrameRules.Fits(parked, Recipe, Answer(AnswerVariant.Grant, amount: 2)).Value.Kind);
        Assert.Equal(ResumeKind.Transition, FrameRules.Fits(parked, Recipe, Answer(AnswerVariant.Text, "use the other API")).Value.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_no_progress_text_works_once()
    {
        var parked = Suspended(Park(ParkReason.NoProgress, ParkKind.Refusal, ResumeKind.Rerun));
        var text = Answer(AnswerVariant.Text, "use the other API");

        Assert.Equal(ResumeKind.Rerun, FrameRules.Fits(parked, Recipe, text).Value.Kind);
        var popped = FrameRules.Pop(parked, Recipe, child: null, T0.AddHours(1)).Value.Instance;
        Assert.Equal("SND-WF-003", FrameRules.Fits(popped, Recipe, text).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_budget_from_a_step_runs_rerun()
    {
        var parked = Suspended(Park(ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun));

        Assert.Equal(ResumeKind.Rerun, FrameRules.Fits(parked, Recipe, Answer(AnswerVariant.GrantTokens, amount: 100_000)).Value.Kind);
        Refused(parked, Answer(AnswerVariant.Grant, amount: 1), "--grant-tokens");
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_budget_from_a_gate_refusal_runs_reevaluate()
    {
        var parked = Suspended(Park(ParkReason.Budget, ParkKind.Step, ResumeKind.Reevaluate, CheckFailed()));

        var resume = FrameRules.Fits(parked, Recipe, Answer(AnswerVariant.GrantTokens, amount: 50_000)).Value;

        Assert.Equal(ResumeKind.Reevaluate, resume.Kind);
        Assert.Equal(CheckFailed(), resume.Withheld);
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_needs_answer_from_a_step_runs_rerun()
    {
        var parked = Suspended(Park(ParkReason.NeedsAnswer, ParkKind.Step, ResumeKind.Rerun));

        Assert.Equal(ResumeKind.Rerun, FrameRules.Fits(parked, Recipe, Answer(AnswerVariant.Text, "the spec means v2")).Value.Kind);
        Refused(parked, Answer(AnswerVariant.Resume), "--text");
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_needs_answer_from_a_wait_runs_reevaluate()
    {
        var parked = Suspended(Park(ParkReason.NeedsAnswer, ParkKind.Wait, ResumeKind.Reevaluate, CiFinished()));

        Assert.Equal(ResumeKind.Reevaluate, FrameRules.Fits(parked, Recipe, Answer(AnswerVariant.Resume)).Value.Kind);
        Assert.Equal(ResumeKind.Reevaluate, FrameRules.Fits(parked, Recipe, Answer(AnswerVariant.Resume, "fixed CI")).Value.Kind);
        Refused(parked, Answer(AnswerVariant.Text, "fixed CI"), "--resume");
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_timeout_runs_rearm()
    {
        var parked = Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm));

        Assert.Equal(ResumeKind.Rearm, FrameRules.Fits(parked, Recipe, Answer(AnswerVariant.Resume)).Value.Kind);
        Refused(parked, Answer(AnswerVariant.Grant, amount: 1), "--resume");
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_escalated_timeout_runs_rearm()
    {
        var escalated = Suspended(Escalate(EscalationKind.Timeout, ResumeKind.Rearm));

        Assert.Equal(ResumeKind.Rearm, FrameRules.Fits(escalated, Recipe, Answer(AnswerVariant.Resume)).Value.Kind);
        Refused(escalated, Answer(AnswerVariant.Accept), "--resume");
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_drift_runs_rearm_after_the_approval()
    {
        var parked = Suspended(Park(ParkReason.SpecConflict, ParkKind.Drift, ResumeKind.Rearm));

        Refused(parked, Answer(AnswerVariant.Resume), "sindri recipe approve workflow.frames");
        Assert.Equal(ResumeKind.Rearm, FrameRules.Pop(parked, Recipe, child: null, T0.AddHours(1)).Value.Resume.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_revoked_only_cancels()
    {
        var parked = Suspended(Park(ParkReason.SpecConflict, ParkKind.Revoked, ResumeKind.None));

        Refused(parked, Answer(AnswerVariant.Resume), "sindri workflow cancel T-3/frames/1");
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_child_revoked_needs_the_parent_approval()
    {
        var parked = Suspended(Park(ParkReason.SpecConflict, ParkKind.ChildRevoked, ResumeKind.Rearm));

        Refused(parked, Answer(AnswerVariant.Resume), "sindri recipe approve workflow.frames");
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_no_runtime_runs_restart()
    {
        var parked = Suspended(Park(ParkReason.NoRuntime, kind: null, ResumeKind.Restart));

        Assert.Equal(ResumeKind.Restart, FrameRules.Fits(parked, Recipe, Answer(AnswerVariant.Resume)).Value.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_dispatch_runs_reemit()
    {
        var parked = Suspended(Park(ParkReason.SpecConflict, ParkKind.Dispatch, ResumeKind.Reemit));

        Assert.Equal(ResumeKind.Reemit, FrameRules.Fits(parked, Recipe, Answer(AnswerVariant.Resume)).Value.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_escalated_gate_runs_override_or_refuse()
    {
        var escalated = Suspended(Escalate(EscalationKind.Gate, ResumeKind.Override, CheckFailed(), Judge));

        var accept = FrameRules.Fits(escalated, Recipe, Answer(AnswerVariant.Accept)).Value;
        var reject = FrameRules.Fits(escalated, Recipe, Answer(AnswerVariant.Reject)).Value;

        Assert.Equal(new ResumeAction(ResumeKind.Override, CheckFailed(), Judge, Effect: null), accept);
        Assert.Equal(new ResumeAction(ResumeKind.Refuse, CheckFailed(), Judge, Effect: null), reject);
        Refused(escalated, Answer(AnswerVariant.Resume), "--accept");
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_mirrored_child_runs_forward()
    {
        var waiting = Instance(new Frame("await", 7, Run: null, ChildId, [], T0));
        var mirrored = Suspended(MirrorChild(ParkReason.Rounds), waiting);

        Assert.Equal(ResumeKind.Forward, FrameRules.Fits(mirrored, Recipe, Answer(AnswerVariant.Grant, amount: 1)).Value.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_on_a_taken_over_or_running_instance_is_refused()
    {
        var taken = FrameRules.TakeOver(Producing(), "human:cli", Context(T0)).Value;

        Refused(taken, Answer(AnswerVariant.Resume), "sindri workflow handback T-3/frames/1");
        Assert.Equal("SND-WF-003", FrameRules.Fits(Producing(), Recipe, Answer(AnswerVariant.Resume)).Error.Code);
    }

    private static void Refused(WorkflowInstance instance, AnswerGiven answer, string fix)
    {
        var refused = FrameRules.Fits(instance, Recipe, answer);

        Assert.Equal("SND-WF-003", refused.Error.Code);
        Assert.Contains("(fix: ", refused.Error.Message, StringComparison.Ordinal);
        Assert.Contains(fix, refused.Error.Message[refused.Error.Message.IndexOf("(fix: ", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }
}
