using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Graph;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

public sealed class RepoScopeTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_session_sees_the_nodes_its_repo_implements_and_the_edges_between_them()
    {
        var scoped = Maps.Checked(Maps.Fixture()).Graph.ScopedTo(Context.Core.Engine.ContextRepoName.From(FixtureMap.Repo));

        Assert.Equal(string.Join(", ", FixtureMap.ScopedNodes), string.Join(", ", scoped.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal)));
        Assert.All(scoped.Edges, e => Assert.Contains(e.From.Value, FixtureMap.ScopedNodes, StringComparer.Ordinal));
        Assert.Contains(scoped.Nodes, n => string.Equals(n.Key.Value, "code:src/layout/SplitPane.ts", StringComparison.Ordinal));   // why: a session sees its repo's code files, not only the features they implement
        Assert.Equal(FixtureMap.ScopedEdges, scoped.Edges.Count);
        _ = Assert.Single(scoped.Aliases);
        Assert.Empty(scoped.Decisions);
    }

    // A person adds a feature and files the repo's screen under it; the screen holds an action. A session in that repo sees the feature (an ancestor
    // of its screen) and the action (inside its screen), and so a lens on the feature or a question naming the action finds them. Another repo's
    // screen under the same feature stays out: an ancestor brings itself, not its other children.
    [Fact, Trait("Lane", "unit")]
    public void A_session_sees_the_part_of_ancestors_of_its_nodes_and_what_they_contain_but_not_a_shared_features_other_children()
    {
        var graph = new GraphBuilder()
            .Node("code:src/a/Schedule.tsx", "Schedule.tsx")
            .Node("surface:screen/schedule", "Schedule screen", Provenance.Deterministic)
            .Node("feature:self-scheduling", "Self scheduling")
            .Node("action:schedule/book-shift", "Book a shift", Provenance.Model, 0.8)
            .Node("surface:screen/roster", "Roster screen", Provenance.Deterministic)
            .Node("code:src/b/Roster.tsx", "Roster.tsx")
            .Edge("code:src/a/Schedule.tsx", EdgeType.Implements, "surface:screen/schedule", evidence: "repo:web/src/a/Schedule.tsx")
            .Edge("surface:screen/schedule", EdgeType.PartOf, "feature:self-scheduling")
            .Edge("action:schedule/book-shift", EdgeType.PartOf, "surface:screen/schedule")
            .Edge("code:src/b/Roster.tsx", EdgeType.Implements, "surface:screen/roster", evidence: "repo:mobile/src/b/Roster.tsx")
            .Edge("surface:screen/roster", EdgeType.PartOf, "feature:self-scheduling")
            .Build();

        var scoped = graph.ScopedTo(Context.Core.Engine.ContextRepoName.From("web"));

        Assert.Equal(["action:schedule/book-shift", "code:src/a/Schedule.tsx", "feature:self-scheduling", "surface:screen/schedule"], scoped.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.DoesNotContain(scoped.Edges, e => e.From.Value == "surface:screen/roster");
        Assert.Equal(["action:schedule/book-shift"], AnchorMatch.FromText(scoped, "how do I book a shift?").Anchors.Select(k => k.Value), StringComparer.Ordinal);
        Assert.Equal("feature:self-scheduling", AnchorMatch.Resolve(scoped, "Self scheduling").Value.Key.Value, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Another_repo_sees_nothing()
    {
        Assert.Empty(Maps.Checked(Maps.Fixture()).Graph.ScopedTo(Context.Core.Engine.ContextRepoName.From("other")).Nodes);
        Assert.Empty(Maps.Checked(Maps.Fixture()).Graph.ScopedTo(Context.Core.Engine.ContextRepoName.From("aw-split")).Nodes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Linked_intent_is_the_scope_plus_exactly_the_deciding_and_mentioning_records_and_the_records_quoted()
    {
        const string quoted = "linear:issue/0e5a1b00-0000-4000-8000-000000000a01";
        const string other = "linear:issue/0e5a1b00-0000-4000-8000-000000000b01";
        var inDecision = DecisionRow.KeyFor(RecordKey.From(quoted), "q in").Value;
        var outDecision = DecisionRow.KeyFor(RecordKey.From(other), "q out").Value;
        var graph = Maps.Checked(Maps.Parse(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:in\", label: In}", "  - {key: \"feature:out\", label: Out}", "  - {key: \"code:src/in.ts\", label: in.ts, provenance: deterministic}",
            "  - {key: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02\", label: Mentions in}",
            "  - {key: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000b02\", label: Mentions out}",
            "edges:",
            "  - {from: \"code:src/in.ts\", to: \"feature:in\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/in.ts\"}]}",
            "  - {from: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02\", to: \"feature:in\", type: mentions}",
            "  - {from: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000b02\", to: \"feature:out\", type: mentions}",
            $"  - {{from: \"{inDecision}\", to: \"feature:in\", type: decides}}",
            $"  - {{from: \"{outDecision}\", to: \"feature:out\", type: decides}}",
            "decisions:",
            $"  - {{record: \"{quoted}\", quote: \"q in\", wording: In., at: 2026-07-15T10:00:00Z}}",
            $"  - {{record: \"{other}\", quote: \"q out\", wording: Out., at: 2026-07-15T10:00:00Z}}"))).Graph;

        var scope = RepoScope.NodesFor(graph, Context.Core.Engine.ContextRepoName.From(FixtureMap.Repo));
        var linked = RepoScope.WithLinkedIntent(graph, scope);

        string[] expected = ["code:src/in.ts", "feature:in", inDecision, "record:" + quoted, "record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02"];
        Assert.Equal(expected.Order(StringComparer.Ordinal), linked.Order(StringComparer.Ordinal));
        Assert.Empty(RepoScope.WithLinkedIntent(graph, new HashSet<string>(StringComparer.Ordinal)));
    }
}
