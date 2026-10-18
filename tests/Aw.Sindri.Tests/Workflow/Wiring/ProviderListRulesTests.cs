using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>A Judge Step's literal 'providers' narrows the profile's list, so a name the profile can't list, a non-list and a fact are refused at validate time (RCP-BLOCK-003; DT-J2 cr-r2-4).</summary>
[Lane(TestLane.Unit)]
public sealed class ProviderListRulesTests
{
    [Theory]
    [InlineData("[\"claude-cli\", \"rules\"]")]
    [InlineData("[\"jev\"]")]
    [InlineData("[]")]
    public void A_list_of_known_provider_names_is_accepted(string providers) =>
        Assert.Empty(Check(Judge(Literal(providers))));

    [Fact]
    public void An_unknown_provider_name_is_refused_naming_it_and_the_known_ones()
    {
        var line = Assert.Single(Check(Judge(Literal("[\"claude\", \"codex-cli\"]"))).Lines());

        Assert.StartsWith("RCP-BLOCK-003: provider 'claude' isn't one of jev, claude-cli, codex-cli, cursor-cli, rules at ", line, StringComparison.Ordinal);
        Assert.Contains("(states.route.step.with.providers)", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_single_string_is_refused_as_a_non_list() =>
        Assert.StartsWith(
            "RCP-BLOCK-003: parameter 'providers' of 'step.judge.choose' takes a list of strings, numbers or flags, not text at ",
            Assert.Single(Check(Judge(Literal("\"claude-cli\""))).Lines()),
            StringComparison.Ordinal);

    [Fact]
    public void A_fact_is_refused_and_the_profile_key_is_accepted()
    {
        Assert.StartsWith(
            "RCP-BLOCK-003: parameter 'providers' of 'step.judge.choose' takes a list of provider names, not a fact at ",
            Assert.Single(Check(Judge(FactValue("fact:output"))).Lines()),
            StringComparison.Ordinal);
        Assert.Empty(Check(Judge(new WithValue(Literal: null, Fact: null, "workflow.judge.providers"))));
    }

    private static IReadOnlyList<Aw.Sindri.Core.Workflow.Validation.RecipeFinding> Check(Recipe recipe) =>
        CheckBlocks(recipe, new Located("r.yaml", [], Engine.EngineFixture.Catalog));

    private static Recipe Judge(WithValue providers) =>
        Of("route", [("route", Active("step.judge.choose", with: With(("prompt", Literal("\"pick\"")), ("choices", Literal("[\"ui\", \"api\"]")), ("facts", Literal("[\"fact:output\"]")), ("providers", providers)))), ("done", Terminal())],
            [Go("route", "done", when: Outcome("ui"))], inputs: ["fact:output"]);
}
