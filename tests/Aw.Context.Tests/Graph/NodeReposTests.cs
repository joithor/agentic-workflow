using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Graph;

public sealed class NodeReposTests
{
    private static EvidenceRef[] Repo(string path) => [new EvidenceRef(RecordKey.From("repo:" + path))];

    private static GraphNode Node(string key, params EvidenceRef[] evidence) => new(NodeKey.From(key), key, Provenance.Deterministic, 1.0, evidence.Length == 0 ? null : evidence);

    private static GraphEdge Implements(string from, string to, params EvidenceRef[] evidence) =>
        new(NodeKey.From(from), NodeKey.From(to), EdgeType.Implements, Provenance.Deterministic, 1.0, evidence.Length == 0 ? null : evidence);

    private static GraphSnapshot Graph(GraphNode[] nodes, GraphEdge[] edges) => new(nodes, edges, [], []);

    [Fact, Trait("Lane", "unit")]
    public void A_code_file_belongs_to_the_repo_its_edge_evidence_names_and_the_feature_to_the_repo_of_its_files()
    {
        var graph = Graph(
            [Node("feature:tabs"), Node("code:src/tabs.ts"), Node("code:src/tabs.ts.other")],
            [Implements("code:src/tabs.ts", "feature:tabs", Repo("web/src/tabs.ts"))]);

        var index = NodeRepos.Index(graph, mapRepo: null);

        Assert.Equal(("web", "web", null), (index["code:src/tabs.ts"], index["feature:tabs"], index["code:src/tabs.ts.other"]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_feature_implemented_from_two_repos_and_a_node_with_evidence_in_two_repos_belong_to_none()
    {
        var graph = Graph(
            [Node("feature:shared"), Node("code:a.ts"), Node("code:b.ts"), Node("surface:screen/both", [.. Repo("web/a.ts"), .. Repo("api/b.ts")])],
            [Implements("code:a.ts", "feature:shared", Repo("web/a.ts")), Implements("code:b.ts", "feature:shared", Repo("api/b.ts"))]);

        var index = NodeRepos.Index(graph, mapRepo: "web");

        Assert.Equal((null, null), (index["feature:shared"], index["surface:screen/both"]));
        Assert.Equal(("web", "api"), (index["code:a.ts"], index["code:b.ts"]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_node_with_no_evidence_takes_the_repo_the_map_was_built_for_except_a_decision_or_a_record_that_names_none()
    {
        var graph = Graph([Node("feature:lone"), Node("decision:use-sqlite"), Node("record:linear:issue/abc"), Node("record:repo:docs-repo/docs/a.md")], []);

        var index = NodeRepos.Index(graph, mapRepo: "web");

        Assert.Equal(("web", null, null, "docs-repo"), (index["feature:lone"], index["decision:use-sqlite"], index["record:linear:issue/abc"], index["record:repo:docs-repo/docs/a.md"]));
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_map_repo_a_node_with_no_evidence_has_no_repo()
    {
        var index = NodeRepos.Index(Graph([Node("feature:lone")], []), mapRepo: null);

        Assert.Null(index["feature:lone"]);
    }
}
