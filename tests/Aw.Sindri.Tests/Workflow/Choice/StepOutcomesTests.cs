using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Workflow.Choice;

/// <summary>The one resolver of a Step's outcomes (DT spec §8.1) and the choice grammar it reads (§3.2).</summary>
public sealed class StepOutcomesTests
{
    private static readonly string[] _routeChoices = ["ui", "api", "db", "all", "none", "other"];

    [Fact, Trait("Lane", "unit")]
    public void Outcomes_are_the_choice_names_in_order_then_the_fixed_ones()
    {
        var recipe = EvalComposition.Recipe(RouteRecipes.Dev);
        var step = recipe.States["route-evidence"].Step!;

        var outcomes = StepOutcomes.Of(Descriptor("step.judge.choose"), step, recipe);

        Assert.Equal([.. _routeChoices, "uncertain"], outcomes!.Select(o => o.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_criterion_with_colons_keeps_the_name_before_the_first_separator()
    {
        var parse = ChoiceList.Parse(Json("""["api: an HTTP or CLI contract changed: a route, a verb"]"""));

        var choice = Assert.Single(parse.Entries);
        Assert.Equal(("api", "an HTTP or CLI contract changed: a route, a verb"), (choice.Outcome.Value, choice.Criterion));
        Assert.Empty(parse.Faults);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_desk_twin_takes_its_route_states_choices_without_uncertain()
    {
        var recipe = EvalComposition.Recipe(RouteRecipes.Dev);
        var twin = recipe.States["route-evidence-desk"].Step!;

        var outcomes = StepOutcomes.Of(Descriptor("step.desk.choose"), twin, recipe);

        Assert.Equal(_routeChoices, outcomes!.Select(o => o.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_descriptor_without_outcomes_from_gives_its_fixed_outcomes()
    {
        var recipe = EvalComposition.Recipe("sindri/workflow.step.yaml");
        var step = recipe.States["produce"].Step!;

        var outcomes = StepOutcomes.Of(Descriptor("step.produce"), step, recipe);

        Assert.Equal(["done"], outcomes!.Select(o => o.Value), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""["ui no separator"]""")]
    [InlineData("""[{"ui":"unquoted"}]""")]
    [InlineData("""["Ui: wrong case"]""")]
    [InlineData("""[5]""")]
    public void Choices_that_dont_parse_give_null(string choices)
    {
        var recipe = EvalComposition.Recipe(RouteRecipes.Dev);
        var step = recipe.States["route-evidence"].Step!;
        var edited = step with { With = new Dictionary<string, WithValue>(step.With, StringComparer.Ordinal) { ["choices"] = new WithValue(Json(choices), Fact: null, Profile: null) } };

        Assert.Null(StepOutcomes.Of(Descriptor("step.judge.choose"), edited, recipe));
    }

    private static BlockDescriptor Descriptor(string id) => EvalComposition.Catalog.Find(BlockId.From(id))!;

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();
}
