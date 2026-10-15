using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Host.Commands.Recipe;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>The lines 'recipe show' adds under the recipe's line: urgency, inputs, outputs, then the path order (spec §6.3, §3.10).</summary>
public sealed class RecipeLineTextTests
{
    private static RecipeSummary Summary(IReadOnlyList<RecipeInput> inputs, IReadOnlyList<RecipeOutput> outputs, string urgency = "work") =>
        new(RecipeId.From("workflow.t"), 1, RecipeHash.From("sha256:" + new string('a', 64)), RecipeStanding.Unapproved, Via: null, [], inputs, urgency, outputs,
            "path", [new StatePlace("a", Order: 1, BranchOf: null), new StatePlace("b", Order: null, BranchOf: "a"), new StatePlace("done", Order: 2, BranchOf: null), new StatePlace("orphan", Order: null, BranchOf: null)],
            [new PathLoop("rounds", "a", "a"), new PathLoop(Name: null, "b", "a")]);

    [Fact, Trait("Lane", "unit")]
    public void The_detail_shows_the_effective_kind_declared_or_implied_by_the_type_and_lists_urgency_then_each_input_then_each_output_then_the_path_order()
    {
        var summary = Summary(
            [new RecipeInput(FactKey.From("fact:item"), "Item", "item", "item", Generate: null), new RecipeInput(FactKey.From("fact:note"), Label: null, Type: null, Kind: null, Generate: null), new RecipeInput(FactKey.From("fact:doc"), Label: null, Type: "file", Kind: null, Generate: null), new RecipeInput(FactKey.From("fact:hook"), Label: null, Type: "payload", Kind: null, Generate: null), new RecipeInput(FactKey.From("fact:repo"), Label: null, Type: "payload", Kind: "webhook", Generate: null)],
            [new RecipeOutput(FactKey.From("fact:draft"), "Draft", "file", "a"), new RecipeOutput(FactKey.From("fact:plain"), Label: null, Type: null, "b")],
            urgency: "triage");

        var text = RecipeLineText.Detail(summary);

        Assert.Equal(
            "urgency: triage\n"
            + "input: fact:item type=item kind=item\n"
            + "input: fact:note type=text kind=text\n"
            + "input: fact:doc type=file kind=file\n"
            + "input: fact:hook type=payload kind=-\n"
            + "input: fact:repo type=payload kind=webhook\n"
            + "output: fact:draft type=file from=a\n"
            + "output: fact:plain type=text from=b\n"
            + "order: path\n"
            + "state: a order=1\nstate: b branch-of=a\nstate: done order=2\nstate: orphan order=-\n"
            + "loop: rounds a -> a\nloop: - b -> a\n",
            text);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_with_no_inputs_or_outputs_prints_only_its_urgency_and_path_order()
    {
        var text = RecipeLineText.Detail(Summary([], []) with { States = [], Loops = [] });

        Assert.Equal("urgency: work\norder: path\n", text);
    }
}
