using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// The park bookkeeping (engine spec rev 8.1 §3.8, §5.8; PR 232 review run 2): (a) a refused effect is held by exactly
/// one spec-conflict park until a resume or approval re-sends it; (b) a takeover never drops a park; (c) a revoke or
/// drift wake always ends in a spec-conflict park; (d) a drift approval ends the drift park wherever it sits and re-sends
/// its effects when the instance would otherwise resume. Every case runs the real rules through 'Decide'.
/// </summary>
public sealed class ParkBookkeepingTests
{
    private const string Dispatcher = "system:dispatcher";
    private const string Engine = "service:engine";
    private static readonly EffectIdentity _e1 = new(Id, Entry: 3, Index: 0);
    private static readonly EffectIdentity _e2 = new(Id, Entry: 3, Index: 1);
    private static readonly RefusedEffect _first = new(BlockId.From("effect.claim.ref"), _e1, new Dictionary<string, WithValue>(StringComparer.Ordinal));
    private static readonly RefusedEffect _second = new(BlockId.From("effect.notify.desk"), _e2, new Dictionary<string, WithValue>(StringComparer.Ordinal));

    /// <summary>(b) A takeover over a timeout park keeps the dispatch park that waited behind it: the handback and the resume reach it, and its resume re-sends the effect.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_takeover_over_a_timeout_park_with_a_dispatch_park_pending_loses_neither()
    {
        var parked = Refuse(TimedOut(), _first, "SND-WF-011");
        Assert.Equal(ParkKind.Dispatch, parked.PendingPark!.ParkKind);

        var back = Applied(Applied(parked, Takeover()), Handback());
        var sent = ResumeEverything(back);

        Assert.Equal([_e1], sent);
    }

    /// <summary>(a) A refusal under a timeout park, then a takeover and a handback: a dispatch park carrying the effect surfaces.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_refusal_under_a_timeout_park_survives_a_takeover_and_the_handback()
    {
        var taken = Applied(TimedOut(), Takeover());
        var refused = Refuse(taken, _first, "SND-WF-011");

        var back = Applied(refused, Handback());

        Assert.Equal(ParkReason.Timeout, back.Suspension!.Reason);
        Assert.Equal([_e1], ResumeEverything(back));
    }

    /// <summary>(a) A refusal that arrives while the instance is parked for a timeout and taken over after is not 'already parked for drift': a dispatch park carries it.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_dispatch_refusal_under_a_timeout_park_is_held_by_a_dispatch_park_not_absorbed_as_drift()
    {
        var withPending = Refuse(TimedOut(), _first, "SND-WF-011");
        var again = Decide(withPending, Item, new OutboxRefused(Dispatcher, _second, "SND-WF-011", Sequence: 92), rules: EngineRules.Default);

        var after = Assert.IsType<Applied>(again).Changes[0].After;
        Assert.Equal([_e1, _e2], ResumeEverything(after));
    }

    /// <summary>(c) A revoke wake over a timeout park taken over: a revoke park surfaces (it replaces a takeover by design).</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_revoke_wake_while_taken_over_ends_in_a_revoke_park()
    {
        var taken = Applied(TimedOut(), Takeover());

        var revoked = Applied(taken, new RecipeWake(Engine, RecipeWakeKind.Revoked, 7));

        Assert.Equal((ParkReason.SpecConflict, ParkKind.Revoked), (revoked.Suspension!.Reason, revoked.Suspension.ParkKind));
    }

    /// <summary>(c) A drift wake over a timeout park that has a dispatch park pending still parks for drift.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_drift_wake_with_other_parks_present_still_ends_in_a_drift_park()
    {
        var parked = Refuse(TimedOut(), _first, "SND-WF-011");

        var drifted = Applied(parked, new RecipeWake(Engine, RecipeWakeKind.Drifted, 7));

        Assert.Contains(AllParks(drifted), static p => p.ParkKind == ParkKind.Drift);
    }

    /// <summary>(d) A re-approval while the drift park is pending behind a takeover ends it; the handback then resumes and re-sends the effect.</summary>
    [Fact, Trait("Lane", "unit")]
    public void A_drift_approval_while_taken_over_ends_the_pending_drift_park_and_the_handback_resends()
    {
        var drifted = Refuse(Claimed(), _first, "SND-RECIPE-006");
        var taken = Applied(drifted, Takeover());

        var approved = Applied(taken, new RecipeWake(Engine, RecipeWakeKind.Approved, 8));
        var back = Assert.IsType<Applied>(Decide(approved, Item, Handback(), rules: EngineRules.Default));

        Assert.Equal(EngineStates.TakenOver, approved.State);
        Assert.Null(back.Changes[0].After.Suspension);
        Assert.Equal([_e1], [.. back.Effects.Select(static e => e.Identity)]);
    }

    /// <summary>(a) Two different effects refused under a timeout park share one dispatch park; the same effect twice is held once.</summary>
    [Fact, Trait("Lane", "unit")]
    public void Two_effects_refused_under_a_timeout_park_are_held_together_and_the_same_one_twice_once()
    {
        var one = Refuse(TimedOut(), _first, "SND-WF-011");
        var two = Refuse(one, _second, "SND-WF-011");

        _ = Assert.IsType<Absorbed>(Decide(two, Item, new OutboxRefused(Dispatcher, _first, "SND-WF-011", Sequence: 95), rules: EngineRules.Default));
        Assert.Equal(1, AllParks(two).Count(static p => p.ParkKind == ParkKind.Dispatch));
        Assert.Equal([_e1, _e2], ResumeEverything(two));
    }

    private static WorkflowInstance Claimed() => At(Item, "claimed", entry: 3);

    private static WorkflowInstance TimedOut() => Suspensions.Suspend(Claimed(), DeskTimeouts.TimeoutPark("claimed", TimeSpan.FromDays(2), "service:workflow.timers"));

    private static WorkflowInstance Refuse(WorkflowInstance instance, RefusedEffect effect, string code) =>
        Applied(instance, new OutboxRefused(Dispatcher, effect, code, Sequence: 91));

    private static WorkflowInstance Applied(WorkflowInstance instance, Trigger trigger) =>
        Assert.IsType<Applied>(Decide(instance, Item, trigger, rules: EngineRules.Default)).Changes[0].After;

    /// <summary>Every park the instance holds, current first.</summary>
    private static IEnumerable<Suspension> AllParks(WorkflowInstance instance) =>
        Suspensions.PendingParks(instance).Prepend(instance.Suspension!).Where(static p => p.Kind == SuspensionKind.Park);

    /// <summary>Answers '--resume' until the instance runs again and returns the identities it re-sent, in order.</summary>
    private static EffectIdentity[] ResumeEverything(WorkflowInstance start)
    {
        var sent = new List<EffectIdentity>();
        var instance = start;
        for (var round = 0; round < 6 && instance.Suspension is not null; round++)
        {
            var applied = Assert.IsType<Applied>(Decide(instance, Item, Resume(), rules: EngineRules.Default));
            sent.AddRange(applied.Effects.Select(static e => e.Identity));
            instance = applied.Changes[0].After;
        }

        Assert.Null(instance.Suspension);
        return [.. sent];
    }

    private static AnswerGiven Resume() =>
        new("human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), Sha("resume")), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);

    private static TakeoverRequested Takeover() => new("human:cli", Stamp('a'));

    private static HandbackRequested Handback() => new("human:cli", Stamp('b'));

    private static CommandStamp Stamp(char digit) => new(CommandNonce.From(new string(digit, 32)), new string('0', 64));
}
