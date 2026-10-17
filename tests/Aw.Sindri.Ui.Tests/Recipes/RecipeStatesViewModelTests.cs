using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Recipes;

namespace Aw.Sindri.Ui.Tests.Recipes;

/// <summary>The recipe's states band (screens spec §4.3, U10a): every mark a served field, nothing worked out from the recipe.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipeStatesViewModelTests
{
    private static JsonNode Golden(string id) => GoldenFiles.Node("workflow-recipe", id);

    private static string Text(JsonNode? node) => (string)node!;

    private static RecipeStatesViewModel Band(string id) => new(FixtureInstances.Recipe(id));

    public static TheoryData<string> Ids => [.. FixtureInstances.RecipeIds];

    private static RecipeStateNodeViewModel Node(RecipeStatesViewModel band, string state) =>
        band.Nodes.Concat(band.Nodes.SelectMany(n => n.Branches)).First(n => n.State == state);

    [Theory]
    [MemberData(nameof(Ids))]
    public void States_in_served_order(string id)
    {
        var served = Golden(id)["states"]!.AsArray().Where(s => s!["branchOf"] is null).Select(s => Text(s!["id"]));

        Expect.Sequence(served, Band(id).Nodes.Select(n => n.State));
    }

    [Fact]
    public void Branch_folds_under_its_source()
    {
        var band = Band("workflow.fork-demo");
        var branch = Golden("workflow.fork-demo")["states"]!.AsArray().Single(s => s!["branchOf"] is not null)!;
        var (id, source) = (Text(branch["id"]), Text(branch["branchOf"]));

        Assert.DoesNotContain(band.Nodes, n => n.State == id);
        Expect.Sequence([id], Node(band, source).Branches.Select(b => b.State));
        Assert.Contains(Labels.BranchOf(source), Node(band, id).Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Loop_pair_draws_the_loop_mark_and_cap()
    {
        var loop = Golden("workflow.dev-desk-round-trip")["loops"]![0]!;
        var (name, cap, from, to) = (Text(loop["name"]), loop["cap"]!.GetValue<int>(), Text(loop["from"]), Text(loop["to"]));
        var band = Band("workflow.dev-desk-round-trip");

        // the loop joins neighbours, so it is the link of the earlier one of the two
        var first = band.Nodes.First(n => n.State == from || n.State == to);
        var second = first.State == from ? to : from;
        Assert.Equal(Labels.LoopLink(name, cap), first.Link);
        Assert.Contains(Labels.LoopsWith(second), first.Name, StringComparison.Ordinal);
        Assert.Contains(Labels.LoopCapWords(name, cap), first.Name, StringComparison.Ordinal);
        Assert.Empty(first.Notes);
    }

    [Fact]
    public void Self_loop_is_a_note_on_its_state()
    {
        var loop = Golden("workflow.project")["loops"]![0]!;
        var node = Node(Band("workflow.project"), Text(loop["from"]));

        Expect.Sequence([Labels.LoopNoteSelf(Text(loop["name"]), loop["cap"]!.GetValue<int>())], node.Notes);
        Assert.Equal(Labels.StepLink, node.Link);
        Assert.Contains(node.Notes[0], node.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_loops_with_one_name_both_show()
    {
        var loops = Golden("workflow.step")["loops"]!.AsArray();
        var band = Band("workflow.step");

        Assert.Equal(2, loops.Count);
        _ = Assert.Single(loops.Select(l => Text(l!["name"])).Distinct(StringComparer.Ordinal));
        // one joins neighbours (a link), the other reaches back over a state (a note on the state it leaves)
        var shown = band.Nodes.Select(n => n.Link).Where(l => l.StartsWith(Labels.LoopMark[0])).Concat(band.Nodes.SelectMany(n => n.Notes)).ToList();
        Assert.Equal(2, shown.Count);
        Assert.All(shown, s => Assert.Contains(Text(loops[0]!["name"]), s, StringComparison.Ordinal));
    }

    [Fact]
    public void Transition_list_says_so()
    {
        Assert.Equal((Labels.OrderedByTransitions, true), (Band("workflow.fork-demo").OrderNote, Band("workflow.fork-demo").HasOrderNote));
        Assert.All(FixtureInstances.RecipeIds.Where(id => string.Equals(Text(Golden(id)["orderedBy"]), "path", StringComparison.Ordinal)), id => Assert.False(Band(id).HasOrderNote));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Outputs_sit_under_their_from_state(string id)
    {
        var band = Band(id);
        var outputs = Golden(id)["outputs"]!.AsArray();

        foreach (var output in outputs)
        {
            var line = Labels.OutputLine(output!["label"] is { } label ? Text(label) : Text(output["key"]), Text(output["type"]));
            Assert.Contains(line, Node(band, Text(output["from"])).Outputs, StringComparer.Ordinal);
        }

        Assert.Equal(outputs.Count, band.Nodes.Concat(band.Nodes.SelectMany(n => n.Branches)).Sum(n => n.Outputs.Count));
    }

    [Fact]
    public void No_terminal_mark_is_invented()
    {
        var band = Band("workflow.item");

        Assert.All(band.Nodes, n => Assert.Equal(n.State, n.Name));
        Assert.All(band.Nodes.SkipLast(1), n => Assert.Equal(Labels.StepLink, n.Link));
        Assert.Equal(string.Empty, band.Nodes[^1].Link);
    }
}
