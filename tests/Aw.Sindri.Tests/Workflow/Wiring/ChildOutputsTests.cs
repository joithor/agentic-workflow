using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>The outputs a child's end carries (DT-C, DT spec §8.4): each declared output, per terminal outcome, present on every path to it.</summary>
public sealed class ChildOutputsTests
{
    private static readonly OutcomeName _resolved = OutcomeName.From("resolved");
    private static readonly OutcomeName _unresolved = OutcomeName.From("unresolved");
    private static readonly FactKey _output = FactKey.From("fact:output");

    /// <summary>produce -> done (resolved); produce -> dropped (unresolved); both transitions are the caller's.</summary>
    private static Recipe Child(params RecipeTransition[] transitions) =>
        Of("produce", [("produce", Active("step.produce")), ("done", Terminal()), ("dropped", Terminal("unresolved"))], transitions, inputs: [], id: "workflow.kid") with
        {
            Outputs = [OutputOf("fact:output", "produce")],
        };

    [Fact, Trait("Lane", "unit")]
    public void Outputs_produced_on_every_path_to_an_outcome_are_present_on_it()
    {
        var child = Child(
            Go("produce", "done", when: Outcome("done"), produces: ["fact:output"]),
            Go("produce", "dropped", "command.skip"));

        var present = ChildOutputs.PresentOn(child);

        Assert.Equal([_output], ChildOutputs.Declared(child));
        Assert.Equal([_output], present[_resolved]);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_missed_on_one_path_is_absent_on_that_outcome()
    {
        var child = Child(
            Go("produce", "done", when: Outcome("done"), produces: ["fact:output"]),
            Go("produce", "dropped", "command.skip"));

        Assert.Empty(ChildOutputs.PresentOn(child)[_unresolved]);

        var oneMissed = Child(
            Go("produce", "done", when: Outcome("done"), produces: ["fact:output"]),
            Go("produce", "done", "command.skip"));

        Assert.Empty(ChildOutputs.PresentOn(oneMissed)[_resolved]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Cancelled_carries_no_outputs()
    {
        var child = Child(Go("produce", "done", when: Outcome("done"), produces: ["fact:output"]));

        Assert.Empty(ChildOutputs.PresentOn(child)[OutcomeName.From("cancelled")]);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_outcome_with_no_reached_terminal_carries_nothing()
    {
        // 'dropped' is declared but no transition enters it.
        var child = Child(Go("produce", "done", when: Outcome("done"), produces: ["fact:output"]));

        Assert.Empty(ChildOutputs.PresentOn(child)[_unresolved]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_renamed_grandchild_output_counts_under_its_new_key()
    {
        var child = Child(GoProducing("produce", "done", "step.completed", Outcome("done"), Rename("fact:output", "fact:result")));

        Assert.Equal([_output], ChildOutputs.PresentOn(child)[_resolved]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_terminal_states_ending_the_same_outcome_must_both_carry_the_output()
    {
        // produce -> done (resolved, with the output) and produce -> also (resolved, without it).
        var child = Of(
            "produce",
            [("produce", Active("step.produce")), ("done", Terminal()), ("also", Terminal())],
            [Go("produce", "done", when: Outcome("done"), produces: ["fact:output"]), Go("produce", "also", "command.skip")],
            inputs: [],
            id: "workflow.kid") with
        {
            Outputs = [OutputOf("fact:output", "produce")],
        };

        Assert.Empty(ChildOutputs.PresentOn(child)[_resolved]);
    }
}
