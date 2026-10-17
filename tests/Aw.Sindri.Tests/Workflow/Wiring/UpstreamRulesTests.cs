using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>RCP-WIRE-001 (inputs produced on every path, W2), RCP-WIRE-002 (produces from the trigger's source) and RCP-CHILD-001 (the child exists and its inputs are wired), spec §3.4, §3.5, §9.1.</summary>
public sealed class UpstreamRulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_Step_input_missing_on_one_path_names_that_path()
    {
        // The recipe of invalid/RCP-WIRE-001.yaml: produce -> checks without fact:output, which step.checks needs.
        var recipe = Of(
            "produce",
            [("produce", Active("step.produce")), ("checks", Active("step.checks")), ("done", Terminal())],
            [
                Go("produce", "checks", when: Outcome("done")),
                Go("checks", "done", when: Outcome("passed")),
                Go("checks", "produce", when: Outcome("failed"), produces: ["fact:findings"]),
            ],
            id: "workflow.wire");

        Assert.Equal(
            ["RCP-WIRE-001: step 'step.checks' in state 'checks' needs 'fact:output', but the path through transitions[0] from 'produce' doesn't produce it at RCP-WIRE-001.yaml:14:5 (states.checks.step.use); produce it on every path into 'checks', or add it to the recipe's inputs"],
            CheckWiring(recipe, new Located("RCP-WIRE-001.yaml", [("states.checks.step", 14, 5)])).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_route_fact_missing_on_a_path_is_RCP_WIRE_001()
    {
        var yaml = Choice.RouteRecipes.Edit(Choice.RouteRecipes.Compact, "facts: [fact:change]", "facts: [fact:change, fact:missing]");

        var lines = Choice.RouteRecipes.Lines(yaml);

        Assert.Contains(lines, l => l.StartsWith("RCP-WIRE-001: step 'step.judge.choose' in state 'route' needs 'fact:missing', but the start doesn't supply it at ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("RCP-WIRE-001: step 'step.desk.choose' in state 'route-desk' needs 'fact:missing', but ", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_fact_produced_on_only_one_of_two_paths_is_reported()
    {
        // fact:output reaches 'checks' from 'produce', but not from the answer wait 'asked'.
        var recipe = Of(
            "produce",
            [("produce", Active("step.produce")), ("asked", WaitForAnswer("skip")), ("checks", Active("step.checks")), ("done", Terminal())],
            [
                Go("produce", "checks", when: Outcome("done"), produces: ["fact:output"]),
                Go("produce", "asked", "command.skip"),
                Go("asked", "checks", "wait.satisfied", Outcome("answered")),
                Go("checks", "done", when: AnyOf(Outcome("passed"), Outcome("failed"))),
            ]);

        Assert.Equal(
            ["RCP-WIRE-001: step 'step.checks' in state 'checks' needs 'fact:output', but the path through transitions[2] from 'asked' doesn't produce it at r.yaml:1:1 (states.checks.step.use); produce it on every path into 'checks', or add it to the recipe's inputs"],
            CheckWiring(recipe).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void The_initial_state_has_only_the_recipe_inputs()
    {
        var recipe = Of("checks", [("checks", Active("step.checks")), ("done", Terminal())],
            [Go("checks", "done", when: AnyOf(Outcome("passed"), Outcome("failed")))]);

        Assert.Equal(
            ["RCP-WIRE-001: step 'step.checks' in state 'checks' needs 'fact:output', but the start doesn't supply it at r.yaml:1:1 (states.checks.step.use); produce it on every path into 'checks', or add it to the recipe's inputs"],
            CheckWiring(recipe).Lines());
        Assert.Empty(CheckWiring(recipe with { Inputs = [new RecipeInput(Core.Workflow.Ids.FactKey.From("fact:output"), Label: null, Type: null, Kind: null, Generate: null)] }));
    }

    [Fact, Trait("Lane", "unit")]
    public void Gate_effect_with_and_child_inputs_are_checked_where_they_run()
    {
        var recipe = Of(
            "produce",
            [
                ("produce", Active("step.produce", inputs: [Use("input.context.retrieve")])),
                ("running", WaitForChild("workflow.step", ("fact:spec", FactValue("fact:stepSpec")))),
                ("done", Terminal()),
            ],
            [
                Go("produce", "running", when: Outcome("done")) with
                {
                    Gates = [Gate("gate.review.judge", 0.8, With(("question", Literal("\"Done?\""))))],
                    Effects = [Use("effect.claim.ref")],
                },
                Go("running", "done", "wait.satisfied", AnyOf(Outcome("resolved"), Outcome("cancelled"))),
            ]);
        var children = new InMemoryChildRecipes().With(Child(Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done"))], inputs: ["fact:spec"], id: "workflow.step")));

        Assert.Equal(
            [
                "RCP-WIRE-001: input 'input.context.retrieve' in state 'produce' needs 'fact:spec', but the start doesn't supply it at r.yaml:1:1 (states.produce.step.inputs[0].use); produce it on every path into 'produce', or add it to the recipe's inputs",
                "RCP-WIRE-001: child input 'fact:spec' in state 'running' needs 'fact:stepSpec', but the path through transitions[0] from 'produce' doesn't produce it at r.yaml:1:1 (states.running.waitFor.child.inputs.fact:spec); produce it on every path into 'running', or add it to the recipe's inputs",
                "RCP-WIRE-001: gate 'gate.review.judge' on transitions[0] needs 'fact:output', but the start doesn't supply it at r.yaml:1:1 (transitions[0].gates[0].use); produce it on this transition or on every path into 'produce', or add it to the recipe's inputs",
                "RCP-WIRE-001: effect 'effect.claim.ref' on transitions[0] needs 'fact:item', but the start doesn't supply it at r.yaml:1:1 (transitions[0].effects[0].use); produce it on this transition or on every path into 'produce', or add it to the recipe's inputs",
            ],
            CheckWiring(recipe, Located.Nowhere with { Children = children }).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_with_fact_must_be_produced_upstream()
    {
        var recipe = Of("produce", [("produce", Active("step.produce", with: With(("hint", FactValue("fact:hint"))))), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done"))]);

        Assert.Equal(
            ["RCP-WIRE-001: parameter 'hint' of step 'step.produce' in state 'produce' needs 'fact:hint', but the start doesn't supply it at r.yaml:1:1 (states.produce.step.with.hint); produce it on every path into 'produce', or add it to the recipe's inputs"],
            CheckWiring(recipe).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void Produces_must_come_from_the_trigger_source()
    {
        // The recipe of invalid/RCP-WIRE-002.yaml: step.produce records fact:output, not fact:findings.
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done"), produces: ["fact:findings"])], id: "workflow.wire-produces");

        Assert.Equal(
            ["RCP-WIRE-002: transition from 'produce' produces 'fact:findings', which step 'step.produce' doesn't record at RCP-WIRE-002.yaml:15:88 (transitions[0].produces[0]); list only what it records (fact:output), or remove it"],
            CheckWiring(recipe, new Located("RCP-WIRE-002.yaml", [("transitions[0].produces[0]", 15, 88)])).Lines());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("command.skip", "trigger 'command.skip'")]
    [InlineData("step.failed", "trigger 'step.failed'")]
    public void A_trigger_that_records_nothing_produces_nothing(string on, string source)
    {
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done")), Go("produce", "done", on, produces: ["fact:output"])]);

        Assert.Equal(
            [$"RCP-WIRE-002: transition from 'produce' produces 'fact:output', which {source} doesn't record at r.yaml:1:1 (transitions[1].produces[0]); remove it, since {source} records no facts"],
            CheckWiring(recipe).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void Events_and_answers_record_their_facts()
    {
        var recipe = Of(
            "triaged",
            [("triaged", WaitForEvent("item.routed")), ("asked", WaitForAnswer("go")), ("done", Terminal()), ("dropped", Terminal("unresolved"))],
            [
                Go("triaged", "asked", "wait.satisfied", Outcome("startable"), ["fact:stepSpec"]),
                Go("triaged", "dropped", "wait.satisfied", Outcome("notStartable")),
                Go("asked", "done", "wait.satisfied", Outcome("answered"), ["fact:answer"]),
            ]);

        Assert.Empty(CheckWiring(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_missing_child_recipe_is_reported()
    {
        // The recipe of invalid/RCP-CHILD-001.yaml.
        var recipe = Of("running", [("running", WaitForChild("workflow.absent")), ("done", Terminal())],
            [Go("running", "done", "wait.satisfied")], id: "workflow.child-missing");

        Assert.Equal(
            ["RCP-CHILD-001: child recipe 'workflow.absent' isn't registered or next to this recipe at RCP-CHILD-001.yaml:8:25 (states.running.waitFor.child.recipe); register it, or put workflow.absent.yaml in this recipe's directory"],
            CheckWiring(recipe, new Located("RCP-CHILD-001.yaml", [("states.running.waitFor.child.recipe", 8, 25)])).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_input_the_parent_doesnt_give_is_reported()
    {
        var recipe = Of("running", [("running", WaitForChild("workflow.step")), ("done", Terminal()), ("dropped", Terminal("unresolved"))],
            [Go("running", "done", "wait.satisfied", Outcome("resolved")), Go("running", "dropped", "wait.satisfied", Outcome("cancelled"))]);
        var children = new InMemoryChildRecipes().With(Child(Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done"))], inputs: ["fact:spec"], id: "workflow.step")));

        Assert.Equal(
            ["RCP-CHILD-001: child 'workflow.step' needs input 'fact:spec', which state 'running' doesn't give it at r.yaml:1:1 (states.running.waitFor.child.inputs); add \"fact:spec\": { fact: <a fact> } or a literal to its inputs"],
            CheckWiring(recipe, Located.Nowhere with { Children = children }).Lines());
    }

    /// <summary>workflow.kid: declares fact:output, produced on the way to resolved and not to unresolved.</summary>
    private static InMemoryChildRecipes Kid(params string[] declared) => new InMemoryChildRecipes().With(Child(
        Of(
            "produce",
            [("produce", Active("step.produce")), ("done", Terminal()), ("dropped", Terminal("unresolved"))],
            [Go("produce", "done", when: Outcome("done"), produces: ["fact:output"]), Go("produce", "dropped", "command.skip")],
            inputs: [],
            id: "workflow.kid") with
        {
            Outputs = [.. declared.Select(d => OutputOf(d, "produce"))],
        }));

    /// <summary>A parent waiting on workflow.kid; <paramref name="onResolved"/> is the transition taken when it resolves.</summary>
    private static Recipe Parent(params ProducedFact[] onResolved) =>
        Of(
            "running",
            [("running", WaitForChild("workflow.kid")), ("done", Terminal()), ("dropped", Terminal("unresolved"))],
            [
                GoProducing("running", "done", "wait.satisfied", Outcome("resolved"), onResolved),
                Go("running", "dropped", "wait.satisfied", AnyOf(Outcome("unresolved"), Outcome("cancelled"))),
            ],
            inputs: []);

    private static Located WithKid(InMemoryChildRecipes kid) => Located.Nowhere with { Children = kid };

    [Fact, Trait("Lane", "unit")]
    public void A_childs_declared_output_may_be_produced_by_the_parent()
        => Assert.Empty(CheckWiring(Parent(Bare("fact:output")), WithKid(Kid("fact:output"))));

    [Fact, Trait("Lane", "unit")]
    public void An_undeclared_child_fact_is_RCP_WIRE_002()
    {
        Assert.Equal(
            ["RCP-WIRE-002: transition from 'running' produces 'fact:findings', which child 'workflow.kid' doesn't declare as an output at r.yaml:1:1 (transitions[0].produces[0]); list only its outputs (fact:output), or declare 'fact:findings' in workflow.kid's outputs"],
            CheckWiring(Parent(Bare("fact:findings")), WithKid(Kid("fact:output"))).Lines());
        Assert.Equal(
            ["RCP-WIRE-002: transition from 'running' produces 'fact:output', which child 'workflow.kid' declares no outputs at r.yaml:1:1 (transitions[0].produces[0]); remove it, or declare it in workflow.kid's outputs"],
            CheckWiring(Parent(Bare("fact:output")), WithKid(Kid())).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_renamed_child_output_is_checked_by_its_source_key()
    {
        Assert.Empty(CheckWiring(Parent(Rename("fact:exported", "fact:output")), WithKid(Kid("fact:output"))));
        Assert.Equal(
            ["RCP-WIRE-002: transition from 'running' produces 'fact:exported' from 'fact:missing', which child 'workflow.kid' doesn't declare as an output at r.yaml:1:1 (transitions[0].produces[0].from); list only its outputs (fact:output), or declare 'fact:missing' in workflow.kid's outputs"],
            CheckWiring(Parent(Rename("fact:exported", "fact:missing")), WithKid(Kid("fact:output"))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_a_childs_output_can_be_renamed()
    {
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [GoProducing("produce", "done", "step.completed", Outcome("done"), Rename("fact:exported", "fact:output"))]);

        Assert.Equal(
            ["RCP-WIRE-002: transition from 'produce' renames 'fact:output' to 'fact:exported', but only a child's outputs can be renamed at r.yaml:1:1 (transitions[0].produces[0]); write the bare key 'fact:output', or rename on a transition from a child wait"],
            CheckWiring(recipe).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_fact_written_twice_on_one_transition_is_RCP_WIRE_002()
    {
        var recipe = Parent(Rename("fact:exported", "fact:output"), Rename("fact:exported", "fact:output"));

        Assert.Equal(
            ["RCP-WIRE-002: transition from 'running' writes 'fact:exported' twice (produces[0] and produces[1]) at r.yaml:1:1 (transitions[0].produces[1]); write each fact once; rename one of them"],
            CheckWiring(recipe, WithKid(Kid("fact:output"))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bare_key_written_twice_on_a_Step_transition_is_RCP_WIRE_002()
    {
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done"), produces: ["fact:output", "fact:output"])]);

        Assert.Equal(
            ["RCP-WIRE-002: transition from 'produce' writes 'fact:output' twice (produces[0] and produces[1]) at r.yaml:1:1 (transitions[0].produces[1]); write each fact once; rename one of them"],
            CheckWiring(recipe).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bare_key_and_a_rename_writing_the_same_key_is_RCP_WIRE_002()
    {
        var recipe = Parent(Bare("fact:output"), Rename("fact:output", "fact:other"));

        Assert.Equal(
            ["RCP-WIRE-002: transition from 'running' writes 'fact:output' twice (produces[0] and produces[1]) at r.yaml:1:1 (transitions[0].produces[1]); write each fact once; rename one of them"],
            CheckWiring(recipe, WithKid(Kid("fact:output", "fact:other"))).Lines().Where(l => l.StartsWith("RCP-WIRE-002", StringComparison.Ordinal)), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_output_satisfies_a_parent_steps_input()
    {
        // step.checks needs fact:output; only the child's end can supply it here.
        var recipe = Of(
            "running",
            [("running", WaitForChild("workflow.kid")), ("checks", Active("step.checks")), ("done", Terminal()), ("dropped", Terminal("unresolved"))],
            [
                GoProducing("running", "checks", "wait.satisfied", Outcome("resolved"), Bare("fact:output")),
                Go("running", "dropped", "wait.satisfied", AnyOf(Outcome("unresolved"), Outcome("cancelled"))),
                Go("checks", "done", when: Outcome("passed")),
                Go("checks", "dropped", when: Outcome("failed")),
            ],
            inputs: []);

        Assert.Empty(CheckWiring(recipe, WithKid(Kid("fact:output"))));
    }
}
