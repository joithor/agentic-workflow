using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The states band (screens §4.2, U8 T3): the recipe's states in the order the API serves them, every mark a served field.</summary>
[Lane(TestLane.Unit)]
public sealed class StatesBandViewModelTests
{
    private static StatesBandViewModel Band(InstanceDetail d) => new(d.States, d.OrderedBy, d.State);

    private static IEnumerable<(string Scenario, InstanceDetail Detail)> All => FixtureInstances.Details;

    private static StateNodeViewModel Node(StatesBandViewModel band, string state) =>
        band.Nodes.Concat(band.Nodes.SelectMany(n => n.Branches)).First(n => n.State == state);

    [Theory]
    [InlineData("dev-desk-round-trip")]
    [InlineData("dev-triage-three-state")]
    public void Nodes_are_the_served_states_in_order(string scenario)
    {
        var detail = FixtureInstances.Detail(scenario);

        var band = Band(detail);

        Expect.Sequence(detail.States.Select(s => s.State), band.Nodes.Select(n => n.State));
        Assert.All(All, one => Expect.Sequence(one.Detail.States.Where(s => s.BranchOf is null).Select(s => s.State), Band(one.Detail).Nodes.Select(n => n.State)));
    }

    [Fact]
    public void Loop_pairs_draw_the_loop_mark_between_them()
    {
        var incident = Band(FixtureInstances.Detail("incident-loop"));
        var dev = Band(FixtureInstances.Detail("dev-desk-round-trip"));
        var self = Band(FixtureInstances.Detail("project-build"));

        Assert.Equal(Labels.LoopMark, Node(incident, "mitigate").Link);
        Assert.Equal(Labels.StepLink, Node(incident, "assess").Link);
        Assert.Equal(Labels.LoopMark, Node(dev, "export").Link);
        Assert.Equal(Labels.StepLink, Node(self, "scoping").Link);   // a state that loops into itself is not a pair
        Assert.Equal(string.Empty, incident.Nodes[^1].Link);
    }

    [Fact]
    public void Branches_fold_under_their_source()
    {
        var detail = FixtureInstances.Detail("transition-list");
        var branch = detail.States.First(s => s.BranchOf is not null);

        var band = Band(detail);

        Assert.DoesNotContain(band.Nodes, n => n.State == branch.State);
        Expect.Sequence([branch.State], Node(band, branch.BranchOf!).Branches.Select(n => n.State));
        Assert.Equal(detail.States.Count - 1, band.Nodes.Count);
    }

    [Fact]
    public void Terminal_states_come_last_as_served()
    {
        Assert.All(All, one =>
        {
            var band = Band(one.Detail);
            var terminals = one.Detail.States.Where(s => s is { Terminal: true, BranchOf: null }).Select(s => s.State).ToList();
            Expect.Sequence(terminals, band.Nodes.Where(n => n.IsTerminal).Select(n => n.State));
            Assert.True(band.Nodes.Skip(band.Nodes.Count - terminals.Count).All(n => n.IsTerminal), one.Scenario);
        });
    }

    [Fact]
    public void Ordered_by_transition_list_says_so_in_the_header()
    {
        var fork = Band(FixtureInstances.Detail("transition-list"));
        var path = Band(FixtureInstances.Detail("project-build"));

        Assert.Equal((Labels.OrderedByTransitions, true), (fork.OrderNote, fork.HasOrderNote));
        Assert.Equal((string.Empty, false), (path.OrderNote, path.HasOrderNote));
    }

    [Fact]
    public void Badge_replaces_the_glyph_on_the_interrupted_state()
    {
        var detail = FixtureInstances.Detail("escalated-gate");
        var cell = detail.States.First(s => s.Badge is not null);

        var node = Node(Band(detail), cell.State);

        var badge = Labels.CellBadge(cell.Badge!.Kind, cell.Badge.Reason);
        Assert.StartsWith($"{cell.State} {badge}", node.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("◐", node.Text, StringComparison.Ordinal);
        Assert.True(node.IsCurrent);
    }

    [Fact]
    public void Taken_over_badges_the_covered_state()
    {
        var row = FixtureInstances.Rows.First(r => r.States.Any(s => s.Badge is { Kind: EngineOwnedKind.TakenOver }));
        var cell = row.States.First(s => s.Badge is not null);

        var band = new StatesBandViewModel(row.States, PathOrdering.Path, row.State);

        Assert.Equal(FixtureInstances.Status("taken-over").Header.State, cell.Badge!.Kind == EngineOwnedKind.TakenOver ? "taken-over" : string.Empty);
        Assert.Contains(Labels.CellBadge(cell.Badge.Kind, cell.Badge.Reason), Node(band, cell.State).Text, StringComparison.Ordinal);
        Assert.All(band.Nodes.Where(n => n.State != cell.State), n => Assert.DoesNotContain("▌", n.Text, StringComparison.Ordinal));
    }

    [Fact]
    public void Outcome_shows_on_its_state()
    {
        var detail = FixtureInstances.Detail("incident-loop");
        var cell = detail.States.First(s => s.Outcome is not null);

        var node = Node(Band(detail), cell.State);

        Assert.Equal(cell.Outcome, node.Outcome);
        Assert.Contains(cell.Outcome!, node.Text, StringComparison.Ordinal);
        Assert.Contains(cell.Outcome!, node.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Wraps_without_detaching_links()
    {
        Assert.All(All, one =>
        {
            var nodes = Band(one.Detail).Nodes;
            Assert.All(nodes.Take(nodes.Count - 1), n => Assert.NotEmpty(n.Link));   // a link belongs to the state before it, so a wrap never starts a line with one
            Assert.DoesNotContain(Labels.StepLink, string.Join(' ', nodes.Select(n => n.Text)), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void A_loop_node_and_a_branch_node_say_it_in_words()
    {
        var incident = Band(FixtureInstances.Detail("incident-loop"));
        var detail = FixtureInstances.Detail("transition-list");
        var branch = detail.States.First(s => s.BranchOf is not null);
        var band = Band(detail);

        var loop = Node(incident, "mitigate");
        var next = incident.Nodes[incident.Nodes.ToList().IndexOf(loop) + 1].State;

        Assert.Contains(Labels.LoopsWith(next), loop.Name, StringComparison.Ordinal);
        Assert.Contains(Labels.BranchOf(branch.BranchOf!), Node(band, branch.State).Name, StringComparison.Ordinal);
        Assert.DoesNotContain("loops with", Node(incident, "assess").Name, StringComparison.Ordinal);
    }
}
