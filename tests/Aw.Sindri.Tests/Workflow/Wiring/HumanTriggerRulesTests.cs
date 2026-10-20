using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>RCP-HUMAN-001 (PB spec W23): an effect that acts with a human's authority sits only on a transition a human drives.</summary>
public sealed class HumanTriggerRulesTests
{
    private static readonly Located _at = new("r.yaml", [("transitions[0].effects[0]", 3, 7), ("transitions[1].effects[0]", 4, 7)]);

    private static RecipeTransition Merge(RecipeTransition transition) => transition with { Effects = [Use("effect.test.merge")] };

    private static Core.Workflow.Recipes.Recipe Of(params RecipeTransition[] transitions) => WiringRecipe.Of(
        "ready",
        [("ready", Active("step.produce")), ("approve", Active("step.test.approve")), ("hold", WaitForEvent("item.routed")), ("done", Terminal())],
        transitions);

    [Fact, Trait("Lane", "unit")]
    public void A_step_completed_out_of_an_agent_Step_is_refused()
    {
        var recipe = Of(Merge(Go("ready", "done", when: Outcome("done"))));

        var lines = CheckHumanTrigger(recipe, _at).Lines();

        Assert.Equal(
            ["RCP-HUMAN-001: effect 'effect.test.merge' acts with a human's authority, but transitions[0] (ready -> done on step.completed) isn't a human's act at r.yaml:1:1 (transitions[0].effects[0].use); put it on a command.<verb> transition, a step.completed out of a human-only Step, or a transition that requires a fact only such a transition produces"],
            lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_transition_from_several_states_needs_every_one_human_only()
    {
        var recipe = Of(Merge(Go("approve", "done", when: Outcome("approved")) with { From = ["approve", "ready"] }));

        _ = Assert.Single(CheckHumanTrigger(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_wait_satisfied_with_no_requirement_is_refused()
    {
        var recipe = Of(Merge(Go("hold", "done", on: "wait.satisfied")));

        _ = Assert.Single(CheckHumanTrigger(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_required_fact_nothing_produces_is_refused()
    {
        var recipe = Of(Merge(Go("hold", "done", on: "wait.satisfied", when: Fact("fact:merge", FactPresence.Present))));

        _ = Assert.Single(CheckHumanTrigger(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_required_absent_fact_does_not_carry_the_authority()
    {
        var recipe = Of(
            Merge(Go("hold", "done", on: "wait.satisfied", when: Fact("fact:stepSpec", FactPresence.Absent))),
            Go("approve", "hold", when: Outcome("approved"), produces: ["fact:stepSpec"]));

        _ = Assert.Single(CheckHumanTrigger(recipe));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("valid/human-trigger-command.yaml")]
    [InlineData("valid/human-trigger-human-only.yaml")]
    [InlineData("valid/human-trigger-carried.yaml")]
    public void The_valid_forms_pass_the_validator(string fixture)
    {
        var document = RecipeComposition.ReadClean(RecipeFixtures.Read(fixture), Path.GetFileName(fixture));

        Assert.Empty(RecipeComposition.Errors(document, WiringCatalog.WithTestBlocks, InMemoryChildRecipes.None));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_carrying_state_must_be_entered_only_by_a_command_with_the_effect()
    {
        var recipe = WiringRecipe.Of(
            "ready",
            [("ready", Active("step.produce")), ("merging", WaitForEvent("item.routed")), ("tidy", Active("step.produce")), ("done", Terminal())],
            [
                Merge(Go("ready", "merging", on: "command.merge")),
                Go("ready", "merging", when: Outcome("done")),
                Go("merging", "tidy", on: "wait.satisfied", produces: ["fact:stepSpec"]),
                Merge(Go("tidy", "done", when: Fact("fact:stepSpec", FactPresence.Present))),
            ]);

        _ = Assert.Single(CheckHumanTrigger(recipe));
    }

    private static WhenClause Fact(string key, FactPresence presence) =>
        new(Outcome: null, Core.Workflow.Ids.FactKey.From(key), presence, Loop: null, Below: null, All: null, Any: null, Not: null);
}
