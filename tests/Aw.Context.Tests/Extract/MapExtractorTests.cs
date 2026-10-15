using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Extract;

public sealed class MapExtractorTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");

    private static readonly RepoFile[] _files =
    [
        new("src/commands/ids.ts", "export type CommandId = \"split-vertical\" | \"close-pane\";"),
        new("src/commands/registry.ts", "register({ id: \"split-vertical\", title: \"Split vertically\" });"),
        new("src/keys.ts", "export const keys: Record<string, CommandId> = { \"ctrl+shift+d\": \"split-vertical\" };"),
        new("src/ui/Split.tsx", "export function SplitPane() {}"),
        new("tests/split.ts", "export type CommandId = \"only-in-tests\";"),
        new("README.md", "text"),
    ];

    [Fact, Trait("Lane", "unit")]
    public void Every_found_node_is_deterministic_and_rests_on_the_file_that_defines_it()
    {
        var map = MapExtractor.Extract(_repo, _files);

        var close = map.Nodes.Single(n => n.Key.Value == "surface:command/close-pane");
        Assert.Equal(Provenance.Deterministic, close.Provenance);
        Assert.Equal(["repo:aw-split-panes/src/commands/ids.ts"], close.EvidenceOrEmpty.Select(e => e.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_node_gets_a_file_level_implements_edge_from_its_file()
    {
        var map = MapExtractor.Extract(_repo, _files);

        var edge = Assert.Single(map.Edges, e => e.Type == EdgeType.Implements && e.To.Value == "surface:command/close-pane");
        Assert.Equal("code:src/commands/ids.ts", edge.From.Value);
        Assert.Equal(Provenance.Deterministic, edge.Provenance);
        Assert.Equal(["repo:aw-split-panes/src/commands/ids.ts"], edge.EvidenceOrEmpty.Select(e => e.Key.Value), StringComparer.Ordinal);
        var file = Assert.Single(map.Nodes, n => n.Key.Value == "code:src/commands/ids.ts");
        Assert.Equal((NodeKind.Code, Provenance.Deterministic, "ids.ts"), (file.Key.Kind, file.Provenance, file.Label));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_command_found_in_two_files_is_one_node_with_two_implementing_files_and_the_named_label()
    {
        var map = MapExtractor.Extract(_repo, _files);

        var split = Assert.Single(map.Nodes, n => n.Key.Value == "surface:command/split-vertical");
        Assert.Equal("Split vertically", split.Label);
        Assert.Equal(["code:src/commands/ids.ts", "code:src/commands/registry.ts"],
            map.Edges.Where(e => e.Type == EdgeType.Implements && e.To.Value == "surface:command/split-vertical").Select(e => e.From.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_keybinding_keeps_its_moves_to_edge_only_when_the_command_was_found()
    {
        var map = MapExtractor.Extract(_repo, _files);
        var lonely = MapExtractor.Extract(_repo, [_files[2]]);

        Assert.Contains(map.Edges, e => e is { Type: EdgeType.MovesTo } && e.From.Value == "surface:keybinding/ctrl+shift+d" && e.To.Value == "surface:command/split-vertical");
        Assert.DoesNotContain(lonely.Edges, e => e.Type == EdgeType.MovesTo);
    }

    [Fact, Trait("Lane", "unit")]
    public void Tests_and_non_source_files_contribute_nothing()
    {
        var map = MapExtractor.Extract(_repo, _files);

        Assert.DoesNotContain(map.Nodes, n => n.Key.Value.Contains("only-in-tests", StringComparison.Ordinal));
        Assert.DoesNotContain(map.Nodes, n => n.Key.Value.Contains("README", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_result_is_the_same_whatever_order_the_files_come_in_and_is_ordered_by_key()
    {
        var forward = MapExtractor.Extract(_repo, _files);
        var backward = MapExtractor.Extract(_repo, [.. _files.Reverse()]);

        Assert.Equal(forward.Nodes, backward.Nodes);
        Assert.Equal(forward.Edges, backward.Edges);
        Assert.Equal(forward.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal), forward.Nodes.Select(n => n.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_edge_follows_the_edge_rules_and_has_both_ends_among_the_nodes()
    {
        var map = MapExtractor.Extract(_repo, _files);
        var keys = map.Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(map.Edges);
        Assert.All(map.Edges, e =>
        {
            Assert.True(EdgeRules.Allows(e.Type, e.From.Kind, e.To.Kind), $"{e.From.Value} {e.Type} {e.To.Value}");
            Assert.Contains(e.From.Value, keys);
            Assert.Contains(e.To.Value, keys);
        });
    }

    [Fact, Trait("Lane", "unit")]
    public void No_files_extract_nothing() => Assert.Equal(ExtractedMap.Empty, MapExtractor.Extract(_repo, []));
}
