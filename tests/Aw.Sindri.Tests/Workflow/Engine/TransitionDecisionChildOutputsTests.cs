using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A child's outputs reach the parent's 'wait.satisfied' on any outcome but 'cancelled' (engine P7 plan D4, line spec App. C.6): the parent's
/// 'produces' picks from them, renamed or not. The pure rules decide from the outputs the trigger carries.
/// </summary>
public sealed class TransitionDecisionChildOutputsTests
{
    private static readonly FactKey _findings = FactKey.From("fact:findings");
    private static readonly FactKey _review = FactKey.From("fact:review");
    private static readonly FactKey _buildNotes = FactKey.From("fact:buildNotes");
    private static readonly FactKey _exported = FactKey.From("fact:exported");

    private static Recipe Parent
    {
        get
        {
            var recipe = RecipeComposition.ReadClean(ShippedRecipes.Read("dev/workflow.dev-child-outputs.yaml"), "workflow.dev-child-outputs.yaml").Recipe;
            RecipeTransition[] routes =
            [
                new(["run"], "review", "wait.satisfied", When("resolved"), [], [], [], [new(FactKey.From("fact:output"), From: null), new(_exported, FactKey.From("fact:output"))]),
                new(["run"], "unproven", "wait.satisfied", When("unresolved"), [], [], [], [new(_buildNotes, _findings)]),   // prove -> build (App. C.6)
                new(["run"], "dropped", "wait.satisfied", When("failed"), [], [], [], [new(_buildNotes, _review)]),         // review -> build (App. C.6)
            ];
            return recipe with { Transitions = [.. routes, .. recipe.Transitions.Where(static t => t.From[0] != "run")] };
        }
    }

    private static WhenClause When(string outcome) => new(Outcome: OutcomeName.From(outcome), Fact: null, Is: null, Loop: null, Below: null, All: null, Any: null, Not: null);

    private static Core.Workflow.Engine.Instances.WorkflowInstance Waiting()
    {
        var run = At(Parent, "run");
        return run with { Frame = run.Frame with { Child = ChildId } };
    }

    private static WaitSatisfied Ended(string outcome, params (string Key, string Text)[] outputs) => new(
        "service:workflow.inbox", new ChildWaitSource(ChildId, TriggerKey.For(ChildId, "end")), OutcomeName.From(outcome),
        outputs.ToDictionary(o => FactKey.From(o.Key), o => Fact(o.Text)));

    [Fact, Trait("Lane", "unit")]
    public void An_unresolved_child_outcome_delivers_a_renamed_output()
    {
        var applied = Assert.IsType<Applied>(Decide(Waiting(), Parent, Ended("unresolved", ("fact:findings", "gaps"))));

        var after = applied.Changes[0].After;
        Assert.Equal("unproven", after.State);
        Assert.Equal(Fact("gaps"), after.Facts[_buildNotes]);
        Assert.False(after.Facts.ContainsKey(_findings));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_child_outcome_delivers_a_renamed_output()
    {
        var applied = Assert.IsType<Applied>(Decide(Waiting(), Parent, Ended("failed", ("fact:review", "two blockers"))));

        var after = applied.Changes[0].After;
        Assert.Equal("dropped", after.State);
        Assert.Equal(Fact("two blockers"), after.Facts[_buildNotes]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_resolved_child_outcome_delivers_outputs_under_the_parents_keys()
    {
        var applied = Assert.IsType<Applied>(Decide(Waiting(), Parent, Ended("resolved", ("fact:output", "summary.csv"))));

        var after = applied.Changes[0].After;
        Assert.Equal(Fact("summary.csv"), after.Facts[FactKey.From("fact:output")]);
        Assert.Equal(Fact("summary.csv"), after.Facts[_exported]);
    }
}
