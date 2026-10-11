using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>A new instance (spec §5.1, §5.2), and the engine triggers the decision hands to the frame and child rules (§5.7).</summary>
public sealed class StartAndEngineTriggerTests
{
    [Fact, Trait("Lane", "unit")]
    public void Start_enters_the_initial_state_as_entry_one()
    {
        var applied = Assert.IsType<Applied>(TransitionDecision.Start(Request(), Step, Context(), new FakeEngineRules()));

        var (row, started) = (applied.Changes[0].After, applied.Changes[0].Event);
        Assert.Equal(("produce", 1, 1L, RunToken.For(1, 1)), (row.State, row.Frame.Entry, row.Version, row.Frame.Run!.Token));
        Assert.Equal(new LoopState(1, 3, Last: null), row.Loops["revisions"]);
        Assert.Equal(Fact("Add a --version flag."), row.Facts[FactKey.From("fact:spec")]);
        Assert.Equal((WorkflowEventType.Started, 0L, 1L), (started.Type, started.FromVersion, started.ToVersion));
        Assert.Same(row, started.Started);
        var start = Assert.Single(applied.FollowUps);
        Assert.Equal((FollowUpKind.StartRun, BlockId.From("step.produce")), (start.Kind, start.Step!.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void Start_on_a_revoked_recipe_is_rejected()
    {
        var decision = TransitionDecision.Start(Request(), Step, Context() with { Approval = ApprovalCheck.Revoked }, new FakeEngineRules());

        Assert.Equal("SND-RECIPE-005", Assert.IsType<Rejected>(decision).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Start_requested_taken_over_starts_taken_over()
    {
        var applied = Assert.IsType<Applied>(TransitionDecision.Start(Request() with { TakenOver = true }, Step, Context(), new FakeEngineRules()));

        var row = applied.Changes[0].After;
        Assert.Equal((EngineStates.TakenOver, "produce"), (row.State, row.Suspended[^1].Frame.State));
    }

    [Fact, Trait("Lane", "unit")]
    public void Child_parked_mirrors_the_park_and_resumed_releases_it()
    {
        var running = At(Item, "running", entry: 4) with { Frame = At(Item, "running", entry: 4).Frame with { Child = ChildId } };
        var parked = new ChildChanged("service:workflow.inbox", ChildId, ChildChange.Parked, ParkReason.NoProgress, "same findings twice", TriggerKey.For(ChildId, "run:3.1:abc"));

        var mirrored = Assert.IsType<Applied>(Decide(running, Item, parked)).Changes[0].After;
        var resumed = new ChildChanged("service:workflow.inbox", ChildId, ChildChange.Resumed, Reason: null, Question: null, TriggerKey.For(ChildId, "cmd:x"));
        var released = Assert.IsType<Applied>(Decide(mirrored, Item, resumed)).Changes[0].After;

        Assert.Equal((EngineStates.Parked, ParkReason.NoProgress), (mirrored.State, mirrored.Park));
        Assert.Equal(("running", 4, ChildId), (released.State, released.Frame.Entry, released.Frame.Child));
    }

    [Fact, Trait("Lane", "unit")]
    public void Cancel_is_exempt_from_the_approval_check()
    {
        var cancel = new CancelRequested("human:thor", CancelOrigin.Command, new CommandStamp(CommandNonce.From(new string('b', 32)), Sha("cancel")), Sequence: null, Parent: null);

        var applied = Assert.IsType<Applied>(Decide(At(Step, "produce"), Step, cancel, context: Context() with { Approval = ApprovalCheck.Revoked }));

        Assert.Equal((EngineStates.Cancelled, "cancelled"), (applied.Changes[0].After.State, applied.Changes[0].After.Outcome?.Value));
    }

    private static StartRequest Request() => new(
        TriggerKey.For(Id, "cmd:" + new string('c', 32) + ":start:" + Sha("start")),
        Id,
        InstanceAddress.Root(ItemKey.From("T-3"), Step.Id, 1),
        new PinnedRecipe(Step.Id, Step.Version, Step.Hash!.Value, new Dictionary<RecipeId, RecipeHash>()),
        ItemKey.From("T-3"),
        Attempt: 1,
        Parent: null,
        new Dictionary<FactKey, FactValue> { [FactKey.From("fact:spec")] = Fact("Add a --version flag.") },
        "human:thor",
        TakenOver: false);
}
