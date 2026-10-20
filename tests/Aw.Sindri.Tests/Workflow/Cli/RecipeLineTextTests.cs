using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Host.Commands.Recipe;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>What 'recipe show' prints from the served view: the name, urgency, inputs, outputs, then the path order (spec §6.3, §3.10); and the line 'recipe list' prints.</summary>
public sealed class RecipeLineTextTests
{
    private static readonly RecipeHash _hash = RecipeHash.From("sha256:" + new string('a', 64));

    private static RecipeView View(IReadOnlyList<RecipeInputView> inputs, IReadOnlyList<RecipeOutputView> outputs, RecipeUrgency urgency = RecipeUrgency.Work, string? name = null) =>
        new(RecipeId.From("workflow.t"), _hash, 1, name, inputs, outputs, urgency, PathOrdering.Path,
            [new RecipeStateView("a", Order: 1, BranchOf: null), new RecipeStateView("b", Order: null, BranchOf: "a"), new RecipeStateView("done", Order: 2, BranchOf: null), new RecipeStateView("orphan", Order: null, BranchOf: null)],
            [new RecipeLoopView("rounds", "a", "a", Cap: 3)], "T");

    private static RecipeSummary Summary(IReadOnlyList<RecipeInput> inputs) =>
        new(RecipeId.From("workflow.t"), 1, _hash, RecipeStanding.Unapproved, Via: null, [], inputs, "work", [], "path", [], []);

    [Fact, Trait("Lane", "unit")]
    public void Show_lists_the_id_urgency_name_each_input_each_output_then_the_path_order()
    {
        var view = View(
            [new("fact:item", "Item", FactType.Item, "item", Required: true, Generate: null), new("fact:note", Label: null, FactType.Text, "text", Required: false, "note {start:date}"), new("fact:hook", Label: null, FactType.Payload, "webhook", Required: true, Generate: null)],
            [new("fact:draft", "Draft", FactType.File, "a"), new("fact:plan", Label: null, FactType.ScopeMap, "b")],
            RecipeUrgency.Triage, name: "triage {start:date}");

        var text = RecipeLineText.Show(view);

        Assert.Equal(
            "workflow.t@aaaaaaaaaaaa\n"
            + "urgency: triage\n"
            + "name: triage {start:date}\n"
            + "input: fact:item type=item kind=item\n"
            + "input: fact:note type=text kind=text generate=\"note {start:date}\"\n"
            + "input: fact:hook type=payload kind=webhook\n"
            + "output: fact:draft type=file from=a\n"
            + "output: fact:plan type=scope-map from=b\n"
            + "order: path\n"
            + "state: a order=1\nstate: b branch-of=a\nstate: done order=2\nstate: orphan order=-\n"
            + "loop: rounds a -> a cap=3\n",
            text);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_with_no_inputs_outputs_or_loops_prints_its_id_urgency_and_path_order()
    {
        var text = RecipeLineText.Show(View([], []) with { States = [], Loops = [], OrderedBy = PathOrdering.TransitionList });

        Assert.Equal("workflow.t@aaaaaaaaaaaa\nurgency: work\norder: transition-list\n", text);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_generate_template_is_printed_with_its_quotes_and_backslashes_escaped()
    {
        var view = View([new("fact:note", Label: null, FactType.Text, "text", Required: false, "say \"hi\" \\ {start:date}")], []);

        Assert.Contains("input: fact:note type=text kind=text generate=\"say \\\"hi\\\" \\\\ {start:date}\"\n", RecipeLineText.Show(view), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_legacy_row_in_the_list_names_its_own_fix()
    {
        var summary = Summary([new RecipeInput(FactKey.From("fact:spec"), Label: null, Type: null, Kind: null, Generate: null)]) with { Standing = RecipeStanding.Approved, Stale = [StaleReason.LegacyShape] };

        Assert.EndsWith("stale: legacy-shape (fix: register the current file at a new version and approve it)", RecipeLineText.Line(summary), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Show_prints_the_paired_type_of_a_kind_only_input()
    {
        var recipe = RecipeReaderTests.Bind(RecipeReaderTests.Minimal + "inputs: [{ key: fact:doc, kind: file }]\n");

        var text = RecipeLineText.Show(RecipeViews.Of(recipe, new RecipeVersion(recipe.Id, recipe.Version, _hash)));

        Assert.Contains("input: fact:doc type=file kind=file\n", text, StringComparison.Ordinal);
    }
}
