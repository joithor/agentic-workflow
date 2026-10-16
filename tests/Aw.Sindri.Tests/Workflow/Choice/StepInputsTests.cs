using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Workflow.Choice;

/// <summary>The one resolver of a Step's inputs (DT spec §8.1).</summary>
public sealed class StepInputsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Inputs_are_the_listed_facts_then_the_fixed_ones()
    {
        var recipe = EvalComposition.Recipe(RouteRecipes.Dev);
        var step = recipe.States["route-evidence"].Step!;

        var inputs = StepInputs.Of(EvalComposition.Catalog.Find(BlockId.From("step.judge.choose"))!, step.With, recipe);

        Assert.Equal(["fact:change", "fact:diff"], inputs!.Select(f => f.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_desk_twin_reads_its_route_states_facts()
    {
        var recipe = EvalComposition.Recipe(RouteRecipes.Dev);
        var twin = recipe.States["route-evidence-desk"].Step!;

        var inputs = StepInputs.Of(EvalComposition.Catalog.Find(BlockId.From("step.desk.choose"))!, twin.With, recipe);

        Assert.Equal(["fact:change", "fact:diff"], inputs!.Select(f => f.Value), StringComparer.Ordinal);
    }
}
