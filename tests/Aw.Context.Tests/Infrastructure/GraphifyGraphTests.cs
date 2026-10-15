using Aw.Context.Infrastructure.Extract;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "unit")]
public sealed class GraphifyGraphTests
{
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Graph = """
        {"directed":false,"nodes":[
        {"id":"a","label":"SplitPane","community":0,"source_file":"/snap/src/layout/SplitPane.ts"},
        {"id":"b","label":"PaneLayout","community":0,"source_file":"src/layout/PaneLayout.ts"},
        {"id":"c","label":"TabManager","community":1,"source_file":"src/tabs/TabManager.ts"},
        {"id":"d","label":"Tab","community":1,"source_file":"src/tabs/TabManager.ts"},
        {"id":"e","label":"Lonely","community":2,"source_file":"src/lonely.ts"},
        {"id":"f","label":"Escape","community":3,"source_file":"../../etc/passwd"},
        {"id":"g","label":"Own","community":3,"source_file":"graphify-out/graph.json"},
        {"id":"h","label":"NoFile","community":3}],
        "links":[
        {"source":"a","target":"b","relation":"calls"},{"source":"a","target":"c","relation":"calls"},{"source":"c","target":"d","relation":"calls"}]}
        """;
#pragma warning restore MA0136


    [Fact]
    public void A_community_with_two_files_is_a_cluster_of_their_repo_relative_paths()
    {
        var clusters = GraphifyGraph.Clusters(Graph, ["/snap"]).Value;

        var layout = Assert.Single(clusters);
        Assert.Equal("c0", layout.Id.Value);
        Assert.Equal(["src/layout/PaneLayout.ts", "src/layout/SplitPane.ts"], layout.Files);
        Assert.InRange(layout.Confidence, 0.1, 0.9);
    }

    [Fact]
    public void Single_file_communities_and_paths_outside_the_snapshot_or_in_graphifys_own_folder_are_dropped()
    {
        var files = GraphifyGraph.Clusters(Graph, ["/snap"]).Value.SelectMany(c => c.Files).ToList();

        Assert.DoesNotContain("src/tabs/TabManager.ts", files, StringComparer.Ordinal);   // a community of one file
        Assert.DoesNotContain(files, f => f.Contains("passwd", StringComparison.Ordinal) || f.StartsWith("graphify-out", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"nodes\":\"x\"}")]
    public void Something_that_is_not_a_graph_is_an_error(string json) => Assert.True(GraphifyGraph.Clusters(json, ["/snap"]).IsError);

    [Fact]
    public void The_same_graph_gives_the_same_clusters()
    {
        Assert.Equal(
    GraphifyGraph.Clusters(Graph, ["/snap"]).Value.Select(c => (c.Id.Value, string.Join(',', c.Files), c.Confidence)),
    GraphifyGraph.Clusters(Graph, ["/snap"]).Value.Select(c => (c.Id.Value, string.Join(',', c.Files), c.Confidence)));
    }
}
