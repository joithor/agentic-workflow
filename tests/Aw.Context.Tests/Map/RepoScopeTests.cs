using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

public sealed class RepoScopeTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_session_sees_the_nodes_its_repo_implements_and_the_edges_between_them()
    {
        var scoped = Maps.Checked(Maps.Fixture()).Graph.ScopedTo(FixtureMap.Repo);

        Assert.Equal(string.Join(", ", FixtureMap.ScopedNodes), string.Join(", ", scoped.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal)));
        Assert.All(scoped.Edges, e => Assert.Contains(e.From.Value, FixtureMap.ScopedNodes, StringComparer.Ordinal));
        Assert.Equal(2, scoped.Edges.Count);
        _ = Assert.Single(scoped.Aliases);
        Assert.Empty(scoped.Decisions);
    }

    [Fact, Trait("Lane", "unit")]
    public void Another_repo_sees_nothing()
    {
        Assert.Empty(Maps.Checked(Maps.Fixture()).Graph.ScopedTo("other").Nodes);
        Assert.Empty(Maps.Checked(Maps.Fixture()).Graph.ScopedTo("aw-split").Nodes);
    }
}
