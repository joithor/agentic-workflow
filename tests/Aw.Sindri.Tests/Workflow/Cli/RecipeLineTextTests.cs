using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Host.Commands.Recipe;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>The lines 'recipe show' adds under the recipe's line: urgency, inputs, outputs, then the path order (spec §6.3, §3.10).</summary>
public sealed class RecipeLineTextTests
{
    private static RecipeSummary Summary(IReadOnlyList<RecipeInput> inputs, IReadOnlyList<RecipeOutput> outputs, string urgency = "work", string? name = null) =>
        new(RecipeId.From("workflow.t"), 1, RecipeHash.From("sha256:" + new string('a', 64)), RecipeStanding.Unapproved, Via: null, [], inputs, urgency, outputs,
            "path", [new StatePlace("a", Order: 1, BranchOf: null), new StatePlace("b", Order: null, BranchOf: "a"), new StatePlace("done", Order: 2, BranchOf: null), new StatePlace("orphan", Order: null, BranchOf: null)],
            [new PathLoop("rounds", "a", "a"), new PathLoop(Name: null, "b", "a")], name);

    [Fact, Trait("Lane", "unit")]
    public void The_detail_shows_each_inputs_kind_and_generate_and_lists_urgency_then_the_name_then_each_input_then_each_output_then_the_path_order()
    {
        var summary = Summary(
            [new RecipeInput(FactKey.From("fact:item"), "Item", "item", "item", Generate: null), new RecipeInput(FactKey.From("fact:note"), Label: null, Type: null, Kind: "text", Generate: "note {start:date}"), new RecipeInput(FactKey.From("fact:doc"), Label: null, Type: null, Kind: "file", Generate: null), new RecipeInput(FactKey.From("fact:hook"), Label: null, Type: "payload", Kind: "webhook", Generate: null), new RecipeInput(FactKey.From("fact:page"), Label: null, Type: null, Kind: "link", Generate: null)],
            [new RecipeOutput(FactKey.From("fact:draft"), "Draft", "file", "a"), new RecipeOutput(FactKey.From("fact:plain"), Label: null, Type: null, "b")],
            urgency: "triage", name: "triage {start:date}");

        var text = RecipeLineText.Detail(summary);

        Assert.Equal(
            "urgency: triage\n"
            + "name: triage {start:date}\n"
            + "input: fact:item type=item kind=item\n"
            + "input: fact:note type=text kind=text generate=\"note {start:date}\"\n"
            + "input: fact:doc type=file kind=file\n"
            + "input: fact:hook type=payload kind=webhook\n"
            + "input: fact:page type=link kind=link\n"
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

    [Fact, Trait("Lane", "unit")]
    public void An_input_with_no_kind_prints_a_dash_and_a_legacy_row_names_its_own_fix()
    {
        var summary = Summary([new RecipeInput(FactKey.From("fact:spec"), Label: null, Type: null, Kind: null, Generate: null)], []) with { Standing = RecipeStanding.Approved, Stale = [StaleReason.LegacyShape] };

        Assert.Contains("input: fact:spec type=text kind=-\n", RecipeLineText.Detail(summary), StringComparison.Ordinal);
        Assert.EndsWith("stale: legacy-shape (fix: register the current file at a new version and approve it)", RecipeLineText.Line(summary), StringComparison.Ordinal);
    }
}
