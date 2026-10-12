using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>Deciding a recipe transition (spec §3.5, §5.3, W5, W12, W13, W20), over the shipped workflow.step and workflow.item.</summary>
public sealed class TransitionDecisionTests
{
    [Fact, Trait("Lane", "unit")]
    public void Same_trigger_twice_records_one_event()
    {
        var report = Completed(1, "done", ("fact:output", "Added --version."));
        var first = Assert.IsType<Applied>(Decide(At(Step, "produce"), Step, report));
        var replayed = Context() with { Recorded = new HashSet<TriggerKey> { first.Key } };

        var second = Decide(first.Changes[0].After, Step, report, context: replayed);

        _ = Assert.Single(first.Changes);
        Assert.Equal(first.Key, Assert.IsType<Absorbed>(second).Key);
    }

    [Fact, Trait("Lane", "unit")]
    public void Late_result_from_an_earlier_entry_is_dropped()
    {
        var checks = At(Step, "checks", entry: 2);
        var late = Completed(1, "done", ("fact:output", "Added --version."));

        var decision = Decide(checks, Step, late);

        Assert.Equal(MatchMiss.StaleRun, TransitionDecision.Match(checks, Step, late).Miss);
        Assert.Equal("SND-WF-005", Assert.IsType<Rejected>(decision).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Stale_token_report_records_nothing()
    {
        var rerun = At(Step, "produce", entry: 1, run: 2);

        var decision = Decide(rerun, Step, Completed(1, 1, "done", ("fact:output", "Added --version.")));

        var rejected = Assert.IsType<Rejected>(decision);
        Assert.Equal("SND-WF-005", rejected.Error.Code);
        Assert.Contains("1.2", rejected.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("failed", "fact:output")]
    [InlineData("done", null)]
    [InlineData("done", "fact:findings")]
    public void Malformed_report_is_rejected_without_a_round(string outcome, string? output)
    {
        (string, string)[] outputs = output is null ? [] : output == "fact:findings" ? [("fact:output", "x"), (output, "y")] : [(output, "x")];

        var decision = Decide(At(Step, "produce"), Step, Completed(1, outcome, outputs));

        Assert.Equal("SND-WF-008", Assert.IsType<Rejected>(decision).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Step_without_runtime_and_not_desk_capable_parks()
    {
        var recipe = WithAgentReview();

        var applied = Assert.IsType<Applied>(Decide(At(recipe, "produce"), recipe, Completed(1, "done", ("fact:output", "x"))));

        var after = applied.Changes[0].After;
        Assert.Equal(EngineStates.Parked, after.State);
        Assert.Equal(ParkReason.NoRuntime, after.Park);
        Assert.Equal(("review", 2), (after.Suspended[^1].Frame.State, after.Suspended[^1].Frame.Entry));
        Assert.Equal(ResumeKind.Restart, after.Suspended[^1].Resume.Kind);
        Assert.Empty(applied.FollowUps);
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_SND_WF_010_runs_restart()
    {
        var recipe = WithAgentReview();
        var real = EngineRules.Default;
        var parked = Assert.IsType<Applied>(Decide(At(recipe, "produce"), recipe, Completed(1, "done", ("fact:output", "x")), rules: real)).Changes[0].After;
        var resume = new AnswerGiven("human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), Sha("resume")), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);

        var later = Context("step.test.agent") with { Now = Now.AddHours(1) };

        var restarted = Assert.IsType<Applied>(Decide(parked, recipe, resume, context: later, rules: real));
        var still = Assert.IsType<Applied>(Decide(parked, recipe, resume, rules: real)).Changes[0].After;

        var after = restarted.Changes[0].After;
        Assert.Equal(("review", 2, RunToken.For(2, 1), StepRuntime.Agent), (after.State, after.Frame.Entry, after.Frame.Run?.Token, after.Frame.Run?.Runtime));
        Assert.Equal(later.Now + recipe.States["review"].Timeout!.After.Span, StateDue(after));
        Assert.Empty(after.Suspended);
        var start = Assert.Single(restarted.FollowUps);
        Assert.Equal((FollowUpKind.StartRun, RunToken.For(2, 1), BlockId.From("step.test.agent")), (start.Kind, start.Run!.Value, start.Step!.Value));
        Assert.Equal((EngineStates.Parked, ParkReason.NoRuntime, ResumeKind.Restart), (still.State, still.Park, still.Suspended[^1].Resume.Kind));
        Assert.Equal(("review", 2), (still.Suspended[^1].Frame.State, still.Suspended[^1].Frame.Entry));
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_SND_WF_010_restarts_on_the_desk_in_a_build_where_the_step_became_desk_capable()
    {
        var recipe = WithAgentReview();
        var real = EngineRules.Default;
        var parked = Assert.IsType<Applied>(Decide(At(recipe, "produce"), recipe, Completed(1, "done", ("fact:output", "x")), rules: real)).Changes[0].After;
        var resume = new AnswerGiven("human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), Sha("resume")), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);
        var deskCatalog = new BlockCatalog([.. Catalog.Blocks.Select(b => b.Id.Value == "step.test.agent" ? b with { DeskCapable = true } : b)], Catalog.Events);
        var later = Context() with { Now = Now.AddHours(1), Catalog = deskCatalog };

        var restarted = Assert.IsType<Applied>(Decide(parked, recipe, resume, context: later, rules: real));

        var after = restarted.Changes[0].After;
        Assert.Equal(("review", RunToken.For(2, 1), StepRuntime.Desk), (after.State, after.Frame.Run?.Token, after.Frame.Run?.Runtime));
        Assert.Equal(later.Now + EngineTimings.Default.DeskTimeout, StateDue(after));
        Assert.Equal(FollowUpKind.StartRun, Assert.Single(restarted.FollowUps).Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void Desk_capable_step_without_runtime_runs_on_the_desk()
    {
        var applied = Assert.IsType<Applied>(Decide(At(Step, "produce"), Step, Completed(1, "done", ("fact:output", "x"))));

        var after = applied.Changes[0].After;
        Assert.Equal(("checks", 2, StepRuntime.Desk), (after.State, after.Frame.Entry, after.Frame.Run!.Runtime));
        var start = Assert.Single(applied.FollowUps);
        Assert.Equal((FollowUpKind.StartRun, RunToken.For(2, 1), BlockId.From("step.checks")), (start.Kind, start.Run!.Value, start.Step!.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void Applied_transition_records_one_event_with_the_version_step_and_its_produces()
    {
        var triaged = At(Item, "triaged");
        var routed = Routed("startable", outputs: ("fact:stepSpec", "Add a --version flag."));

        var applied = Assert.IsType<Applied>(Decide(triaged, Item, routed, Gates(("gate.route.startable", GateVerdict.Pass, null))));

        var (after, recorded) = (applied.Changes[0].After, applied.Changes[0].Event);
        Assert.Equal(("startable", 5L), (after.State, after.Version));
        Assert.Equal(Fact("Add a --version flag."), after.Facts[FactKey.From("fact:stepSpec")]);
        Assert.Equal((WorkflowEventType.Transition, 4L, 5L, 7L), (recorded.Type, recorded.FromVersion, recorded.ToVersion, recorded.Epoch));
        Assert.Equal(("wait.satisfied", "startable"), (recorded.Trigger.Kind, recorded.Trigger.Outcome?.Value));
        Assert.Equal(GateOutcome.Passed, Assert.Single(recorded.Gates).Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void Effects_get_command_ids_derived_from_the_trigger_key()
    {
        var startable = At(Item, "startable", entry: 2);
        var budget = Gates(("gate.budget.available", GateVerdict.Pass, null));

        var first = Assert.IsType<Applied>(Decide(startable, Item, Slot(3), budget));
        var again = Assert.IsType<Applied>(Decide(startable, Item, Slot(3), budget));
        var other = Assert.IsType<Applied>(Decide(startable, Item, Slot(4), budget));

        var command = Assert.Single(first.Outbox);
        Assert.Equal((BlockId.From("effect.claim.ref"), CommandOrigin.Recipe, "WFI-0123456789abcdef0123456789abcdef:3:0"), (command.Effect, command.Origin, command.Identity.Text));
        Assert.Equal(command.Id, Assert.Single(again.Outbox).Id);
        Assert.NotEqual(command.Id, Assert.Single(other.Outbox).Id);
        Assert.Equal([command.Id], first.Changes[0].Event.Outbox);
    }

    [Fact, Trait("Lane", "unit")]
    public void Entering_a_child_wait_creates_the_child_in_the_same_decision()
    {
        var claimed = At(Item, "claimed", entry: 3);
        var created = new WaitSatisfied("service:workflow.inbox", new EventWaitSource(EventKind.From("claim.created"), 52), OutcomeName.From("created"), new Dictionary<FactKey, FactValue>());

        var applied = Assert.IsType<Applied>(Decide(claimed, Item, created));

        Assert.Equal(2, applied.Changes.Count);
        Assert.Equal(("running", ChildId), (applied.Changes[0].After.State, applied.Changes[0].After.Frame.Child));
        var child = applied.Changes[1];
        Assert.Equal((WorkflowEventType.Started, ChildId, "produce", 1), (child.Event.Type, child.After.Id, child.After.State, child.After.Frame.Entry));
        Assert.Contains(applied.FollowUps, f => f.Kind == FollowUpKind.StartRun && f.Instance == ChildId);
    }

    [Fact, Trait("Lane", "unit")]
    public void Step_failed_without_a_transition_parks_needs_answer()
    {
        var failed = new StepFailed("human:thor", RunToken.For(1, 1), "The build broke.", Tokens: 300, Sha("failed"));

        var applied = Assert.IsType<Applied>(Decide(At(Step, "produce"), Step, failed));

        var after = applied.Changes[0].After;
        Assert.Equal((EngineStates.Parked, ParkReason.NeedsAnswer), (after.State, after.Park));
        Assert.Equal((ParkKind.Step, ResumeKind.Rerun), (after.Suspended[^1].Suspension.ParkKind, after.Suspended[^1].Resume.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public void Human_trigger_on_revoked_recipe_gets_005_and_records_nothing()
    {
        var revoked = Context() with { Approval = ApprovalCheck.Revoked };

        var decision = Decide(At(Step, "produce"), Step, Completed(1, "done", ("fact:output", "x")), context: revoked);

        Assert.Equal("SND-RECIPE-005", Assert.IsType<Rejected>(decision).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void System_trigger_on_drifted_recipe_parks_spec_conflict()
    {
        var drifted = Context() with { Approval = ApprovalCheck.Drifted };

        var applied = Assert.IsType<Applied>(Decide(At(Item, "triaged"), Item, Routed("startable"), context: drifted));

        var top = applied.Changes[0].After.Suspended[^1];
        Assert.Equal((ParkReason.SpecConflict, ParkKind.Drift, ResumeKind.Rearm), (top.Suspension.Reason, top.Suspension.ParkKind, top.Resume.Kind));
    }

    private static DateTimeOffset? StateDue(WorkflowInstance instance) =>
        Assert.Single(instance.Frame.Deadlines, d => d.Id.Kind == DeadlineKind.State).Due;

    private static WaitSatisfied Slot(long grant) =>
        new("service:workflow.inbox", new ResourceWaitSource(ResourceKind.Slot, grant), Outcome: null, new Dictionary<FactKey, FactValue>());

    private static Recipe WithAgentReview()
    {
        var recipe = Minimal([From("review", ARecipeWhen("done")), new(["review"], "done", "step.completed", ARecipeWhen("done"), [], [], [], [])]);
        var states = new Dictionary<string, RecipeState>(recipe.States, StringComparer.Ordinal)
        {
            ["review"] = recipe.States["produce"] with { Step = new StepUse(BlockId.From("step.test.agent"), new Dictionary<string, WithValue>(StringComparer.Ordinal), []) },
        };
        return recipe with { States = states };
    }

    private static WhenClause ARecipeWhen(string outcome) => ARecipe.When(OutcomeName.From(outcome));
}
