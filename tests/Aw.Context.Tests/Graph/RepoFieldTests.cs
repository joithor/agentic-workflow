using Aw.Context.Core.Code;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Subgraph;

namespace Aw.Context.Tests.Graph;

/// <summary>Every answer that lists nodes or files says which repo each belongs to (null when it belongs to no single repo).</summary>
public sealed class RepoFieldTests
{
    private const string Feature = "feature:f";
    private const string File = "code:src/web.ts";
    private const string Record = "record:repo:docs/a.md";
    private const string Decision = "decision:d";
    private static readonly DateTimeOffset _asOf = DateTimeOffset.Parse("2026-10-14T09:12:40Z", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly LensBasis _basis = new("v1", Draft: false, _asOf) { MapRepo = "web" };

    private static GraphNode Node(string key, Provenance provenance = Provenance.Human) => new(NodeKey.From(key), key, provenance);

    private static GraphEdge Edge(string from, EdgeType type, string to, params EvidenceRef[] evidence) =>
        new(NodeKey.From(from), NodeKey.From(to), type, Provenance.Human, 1.0, evidence.Length == 0 ? null : evidence);

    private static GraphSnapshot Graph() => new(
        [Node(Feature), Node(File), Node(Record), Node(Decision)],
        [
            Edge(File, EdgeType.Implements, Feature, new EvidenceRef(RecordKey.From("repo:web/src/web.ts"))),
            Edge(Record, EdgeType.Mentions, Feature),
            Edge(Decision, EdgeType.Decides, Feature),
        ],
        [],
        []);

    [Fact, Trait("Lane", "unit")]
    public void The_lenses_name_the_repo_of_every_band_item_and_a_decision_of_no_repo_names_none()
    {
        var view = LensesBuilder.Build(_basis, Graph(), NodeKey.From(Feature), 1).Value;

        Assert.Equal("web", Assert.Single(view.Bands.Product.Own).Repo);
        Assert.Equal("web", Assert.Single(view.Bands.Code.Files).Repo);
        Assert.Equal("docs", Assert.Single(view.Bands.Intent.Records).Repo);
        Assert.Null(Assert.Single(view.Bands.Intent.Decisions).Repo);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_subgraph_names_the_repo_of_every_band_item()
    {
        var view = SubgraphBuilder.Build(_basis, Graph(), [Feature, File, Record, Decision], 0, []).Value;

        Assert.Equal(("web", "web", "docs", null), (view.Bands.Product.Own[0].Repo, view.Bands.Code.Files[0].Repo, view.Bands.Intent.Records[0].Repo, view.Bands.Intent.Decisions[0].Repo));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_code_lens_names_the_repo_of_its_files_and_tests()
    {
        var graph = Graph() with
        {
            Nodes = [.. Graph().Nodes, Node("code:test/web.test.ts")],
            Edges = [.. Graph().Edges, Edge("code:test/web.test.ts", EdgeType.Tests, Feature, new EvidenceRef(RecordKey.From("repo:web/test/web.test.ts")))],
        };

        var view = CodeLensBuilder.Build(_basis, graph, [Feature]).Value;

        Assert.Equal("web", Assert.Single(view.Files).Repo);
        Assert.Equal("web", Assert.Single(view.Tests).Repo);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_review_queue_names_the_repo_of_a_proposed_node_and_of_a_suggested_file_link()
    {
        var draft = new GraphSnapshot(
            [Node(Feature, Provenance.Model), Node(File, Provenance.Model)],
            [new GraphEdge(NodeKey.From(File), NodeKey.From(Feature), EdgeType.Implements, Provenance.Model, 0.6, [new EvidenceRef(RecordKey.From("repo:web/src/web.ts"))])],
            [],
            []);

        var queue = ReviewBacklog.Build("v1", draft, [], mapRepo: "web");

        Assert.NotEmpty(queue.Entries);
        Assert.All(queue.Entries, e => Assert.Equal("web", e.Repo));
    }
}
