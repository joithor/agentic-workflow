using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Wiring;

namespace Aw.Sindri.Tests.ShowRecipe;

/// <summary>The fields the app's recipe view reads (F5 PR A): the recipe's why, each state's kind, outcome texts, inputs and outputs, and every transition.</summary>
public sealed class RecipeViewsFieldsTests
{
    private static readonly RecipeVersion _stamp = new(RecipeId.From("workflow.minimal"), 1, RecipeHash.From("sha256:" + new string('a', 64)));

    private static BlockCatalog Catalog(BlockDescriptor step) => new([step], []);

    private static BlockDescriptor Step(BlockRuntime? runtime, bool deskCapable = true, bool humanOnly = false) =>
        TestBlocks.Step("step.produce") with { Runtime = runtime, DeskCapable = deskCapable, HumanOnly = humanOnly ? true : null };

    private static RecipeStateView Produce(Recipe recipe, BlockCatalog? catalog) =>
        RecipeViews.Of(recipe, _stamp, catalog: catalog).States.Single(static s => s.State == "produce");

    [Fact, Trait("Lane", "unit")]
    public void The_recipe_why_is_on_the_view_and_a_recipe_without_one_leaves_it_out_of_the_json()
    {
        var recipe = ARecipe.Minimal(_stamp.Id);

        Assert.Null(RecipeViews.Of(recipe, _stamp).Why);
        Assert.Equal("Gets the work done.", RecipeViews.Of(recipe with { Why = "Gets the work done." }, _stamp).Why);
        Assert.DoesNotContain("\"why\"", Json(RecipeViews.Of(recipe, _stamp)), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(BlockRuntime.Agent, false, false, RecipeStateKind.Agent)]
    [InlineData(BlockRuntime.Block, true, false, RecipeStateKind.Automatic)]
    [InlineData(BlockRuntime.Desk, true, false, RecipeStateKind.You)]
    [InlineData(null, true, false, RecipeStateKind.You)]
    [InlineData(BlockRuntime.Desk, true, true, RecipeStateKind.OnlyYou)]
    public void A_state_is_agent_automatic_you_or_only_you_by_its_steps_runtime(BlockRuntime? runtime, bool deskCapable, bool humanOnly, RecipeStateKind expected)
    {
        var kind = Produce(ARecipe.Minimal(_stamp.Id), Catalog(Step(runtime, deskCapable, humanOnly))).Kind;

        Assert.Equal(expected, kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_with_no_known_step_or_a_terminal_state_has_no_kind_and_a_child_wait_is_a_sub_workflow()
    {
        var recipe = ARecipe.Minimal(_stamp.Id);
        var views = RecipeViews.Of(recipe, _stamp, catalog: Catalog(Step(runtime: null)));
        var child = recipe with { States = new Dictionary<string, RecipeState>(recipe.States, StringComparer.Ordinal) { ["produce"] = WiringRecipe.WaitForChild("workflow.child") } };

        Assert.Null(Produce(recipe, catalog: null).Kind);
        Assert.Null(views.States.Single(static s => s.State == "done").Kind);
        Assert.Equal(RecipeStateKind.SubWorkflow, Produce(child, catalog: null).Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_kind_serializes_as_its_camel_case_name_and_is_left_out_when_unknown()
    {
        var recipe = ARecipe.Minimal(_stamp.Id);

        var known = Json(Produce(recipe, Catalog(Step(BlockRuntime.Desk, humanOnly: true))));
        var unknown = Json(Produce(recipe, catalog: null));

        Assert.Contains("\"kind\":\"onlyYou\"", known, StringComparison.Ordinal);
        Assert.DoesNotContain("\"kind\"", unknown, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void On_outcome_lists_the_texts_in_the_steps_outcome_order_and_drops_an_empty_one()
    {
        var step = TestBlocks.Step("step.produce") with { Outcomes = [OutcomeName.From("approve"), OutcomeName.From("revise"), OutcomeName.From("drop")] };
        var recipe = ARecipe.Minimal(_stamp.Id);
        var texts = new Dictionary<string, string>(StringComparer.Ordinal) { ["drop"] = "Ends it.", ["approve"] = "Moves on.", ["revise"] = " " };
        recipe = recipe with { States = new Dictionary<string, RecipeState>(recipe.States, StringComparer.Ordinal) { ["produce"] = recipe.States["produce"] with { OnOutcome = texts } } };

        var onOutcome = Produce(recipe, Catalog(step)).OnOutcome;

        Assert.Equal([("approve", "Moves on."), ("drop", "Ends it.")], onOutcome!.Select(static o => (o.Outcome, o.Text)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_that_writes_no_on_outcome_leaves_the_field_out()
    {
        var recipe = ARecipe.Minimal(_stamp.Id);

        Assert.Null(Produce(recipe, Catalog(Step(BlockRuntime.Desk))).OnOutcome);
        Assert.DoesNotContain("onOutcome", Json(Produce(recipe, Catalog(Step(BlockRuntime.Desk)))), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_transition_is_listed_in_file_order_with_its_outcome_and_label_left_out_when_none()
    {
        var recipe = ARecipe.Minimal(_stamp.Id);
        recipe = recipe with
        {
            Transitions = [recipe.Transitions[0] with { Label = "all done" }, WiringRecipe.Go("produce", "done", on: "command.cancel")],
        };

        var transitions = RecipeViews.Of(recipe, _stamp).Transitions!;

        Assert.Equal(
            [("produce", "step.completed", "done", "done", "all done"), ("produce", "command.cancel", null, "done", null)],
            transitions.Select(static t => (string.Join(',', t.From), t.On, t.Outcome, t.To, t.Label)));
        Assert.DoesNotContain("\"outcome\"", Json(transitions[1]), StringComparison.Ordinal);
        Assert.NotNull(RecipeViews.Of(recipe, _stamp).TransitionLabels);   // the labelled list stays as it was
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_lists_the_facts_its_step_reads_and_writes_as_fact_rows_key_them_with_the_recipes_labels()
    {
        var step = TestBlocks.Step("step.produce") with { Inputs = [FactKey.From("fact:item")] };
        var recipe = ARecipe.Minimal(_stamp.Id);
        recipe = recipe with
        {
            Inputs = [new RecipeInput(FactKey.From("fact:item"), "The item", "item", "item", Generate: null)],
            Outputs = [new RecipeOutput(FactKey.From("fact:output"), Label: null, Type: null, From: "produce")],
        };

        var state = Produce(recipe, Catalog(step));

        Assert.Equal([("fact:item", "The item")], state.Inputs!.Select(static f => (f.Key, f.Label)));
        Assert.Equal([("fact:output", null)], state.Outputs!.Select(static f => (f.Key, f.Label)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_step_that_reads_or_writes_nothing_leaves_the_lists_out()
    {
        var step = TestBlocks.Step("step.produce") with { Outputs = [] };

        var state = Produce(ARecipe.Minimal(_stamp.Id), Catalog(step));

        Assert.Null(state.Inputs);
        Assert.Null(state.Outputs);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_step_whose_inputs_come_from_a_list_parameter_lists_those_facts_then_its_fixed_ones()
    {
        var step = TestBlocks.Step("step.produce") with { InputsFrom = "facts", Inputs = [FactKey.From("fact:output")] };
        var recipe = ARecipe.Minimal(_stamp.Id);
        var use = recipe.States["produce"].Step! with { With = WiringRecipe.With(("facts", WiringRecipe.Literal("[\"fact:item\"]"))) };
        recipe = recipe with { States = new Dictionary<string, RecipeState>(recipe.States, StringComparer.Ordinal) { ["produce"] = recipe.States["produce"] with { Step = use } } };

        var inputs = Produce(recipe, Catalog(step)).Inputs;

        Assert.Equal(["fact:item", "fact:output"], inputs!.Select(static f => f.Key), StringComparer.Ordinal);
    }

    private static string Json<T>(T value) => System.Text.Encoding.UTF8.GetString(AwJson.Serialize(value, SliceHarness.Json));
}
