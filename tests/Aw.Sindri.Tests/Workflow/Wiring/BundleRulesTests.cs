using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>LN-WF5 (line spec §5.1, §5.2): a Step's 'bundle' parameter is 'style' or 'lenses' and fits the Step, checked against the descriptor (RCP-BLOCK-003).</summary>
public sealed class BundleRulesTests
{
    private static readonly BlockCatalog _catalog = new(
        [
            .. WiringCatalog.Shipped.Blocks,
            Bundling("step.design.draft"),
            Bundling("step.lens.run"),
        ],
        []);

    [Theory, Trait("Lane", "unit")]
    [InlineData("step.design.draft", "style")]
    [InlineData("step.lens.run", "lenses")]
    public void The_pack_a_Step_bundles_is_accepted(string step, string bundle) =>
        Assert.Empty(Check(step, Literal($"\"{bundle}\"")));

    [Fact, Trait("Lane", "unit")]
    public void A_Step_without_a_bundle_is_accepted() =>
        Assert.Empty(CheckBlocks(RecipeOf("step.lens.run", With()), new Located("r.yaml", [], _catalog)));

    [Fact, Trait("Lane", "unit")]
    public void A_value_that_is_neither_style_nor_lenses_is_refused_naming_both()
    {
        var line = Assert.Single(Check("step.design.draft", Literal("\"other\"")).Lines());

        Assert.StartsWith("RCP-BLOCK-003: parameter 'bundle' of 'step.design.draft' takes 'style' or 'lenses', not 'other' at ", line, StringComparison.Ordinal);
        Assert.Contains("(states.run.step.with.bundle)", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_lens_Step_cannot_bundle_the_style_pack() =>
        Assert.StartsWith(
            "RCP-BLOCK-003: parameter 'bundle' of 'step.lens.run' can only bundle 'lenses', not 'style' at ",
            Assert.Single(Check("step.lens.run", Literal("\"style\"")).Lines()),
            StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_writer_Step_cannot_bundle_the_lens_pack() =>
        Assert.StartsWith(
            "RCP-BLOCK-003: parameter 'bundle' of 'step.design.draft' can only bundle 'style', not 'lenses' at ",
            Assert.Single(Check("step.design.draft", Literal("\"lenses\"")).Lines()),
            StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_fact_is_no_bundle_name() =>
        Assert.StartsWith(
            "RCP-BLOCK-003: parameter 'bundle' of 'step.design.draft' takes the plain word 'style' or 'lenses', not a fact at ",
            Assert.Single(Check("step.design.draft", FactValue("fact:output")).Lines()),
            StringComparison.Ordinal);

    private static IReadOnlyList<Core.Workflow.Validation.RecipeFinding> Check(string step, WithValue bundle) =>
        CheckBlocks(RecipeOf(step, With(("bundle", bundle))), new Located("r.yaml", [], _catalog));

    private static Recipe RecipeOf(string step, Dictionary<string, WithValue> with) =>
        Of("run", [("run", Active(step, with: with)), ("done", Terminal())], [Go("run", "done", when: Outcome("done"))]);

    private static BlockDescriptor Bundling(string id) => new(
        BlockId.From(id), BlockKind.Step, Version: 1, Inputs: [], Outputs: [], Outcomes: [OutcomeName.From("done")], Capability.Tools,
        [new ParameterSpec("bundle", ParameterType.Text, Required: false, IsCapabilityField: false)], Rung: null, DeskCapable: true);
}
