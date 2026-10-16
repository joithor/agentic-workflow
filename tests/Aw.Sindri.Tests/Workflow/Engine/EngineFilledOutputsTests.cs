using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>What the engine writes for an output a report omits (DT spec §8.1): fact:decision of the choice Steps.</summary>
public sealed class EngineFilledOutputsTests
{
    private static IReadOnlyList<OutputSpec> Outputs => EvalComposition.Catalog.Find(BlockId.From("step.desk.choose"))!.Outputs;

    [Fact, Trait("Lane", "unit")]
    public void Desk_fill_writes_choice_provider_desk_and_actor()
    {
        var filled = EngineFilledOutputs.For(Outputs, OutcomeName.From("api"), "desk", "human:alice");

        var decision = Assert.Single(filled);
        Assert.Equal(("fact:decision", """{"choice":"api","provider":"desk","actor":"human:alice"}"""), (decision.Key.Value, decision.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void Eval_fill_writes_provider_eval()
    {
        var filled = EngineFilledOutputs.For(Outputs, OutcomeName.From("api"), "eval", actor: null);

        Assert.Equal("""{"choice":"api","provider":"eval"}""", Assert.Single(filled).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void Outputs_that_are_not_engine_filled_get_nothing()
    {
        var produce = EvalComposition.Catalog.Find(BlockId.From("step.produce"))!.Outputs;

        Assert.Empty(EngineFilledOutputs.For(produce, OutcomeName.From("done"), "desk", "human:alice"));
    }
}
