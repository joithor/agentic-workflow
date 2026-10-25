using Aw.Context.Core.ScopeGraph;
using Xunit;

namespace Aw.Context.Testing.ScopeGraph;

/// <summary>The scope-run store's contract (issue 1005): the fake and the SQLite store pass the same tests.</summary>
public abstract class ScopeRunStoreContract
{
    private const string Instance = "ITM-1/line/3/scoping.1";
    private static readonly DateTimeOffset _at = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>A store to test, empty.</summary>
    /// <returns>The store.</returns>
    protected abstract IScopeRunStore CreateStore();

    private static ScopeGraphView Graph(params string[] ids) =>
        new(1, "v1", [], 1, [], [.. ids.Select((id, i) => new ScopeGraphNode(id, "feature", null, null, id, i + 1, "product", $"L-product-{i + 1}", "own", []))], [], []);

    /// <summary>An instance with no graph holds nothing, not even false.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_instance_with_no_graph_answers_null()
    {
        var held = await CreateStore().Holds(Instance, "feature:a", None);

        Assert.True(held.IsSuccess);
        Assert.Null(held.Value);
    }

    /// <summary>A remembered graph holds its nodes and no others, and a newer graph for the instance replaces it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_remembered_graph_holds_its_nodes_and_the_newest_replaces_the_old()
    {
        var store = CreateStore();

        _ = await store.Remember(Instance, Graph("feature:a", "feature:b"), _at, None);
        var first = (await store.Holds(Instance, "feature:a", None)).Value;
        var absent = (await store.Holds(Instance, "feature:z", None)).Value;
        _ = await store.Remember(Instance, Graph("feature:z"), _at, None);
        var replaced = (await store.Holds(Instance, "feature:a", None)).Value;
        var now = (await store.Holds(Instance, "feature:z", None)).Value;

        Assert.Equal(new bool?[] { true, false, false, true }, new[] { first, absent, replaced, now });
    }

    /// <summary>The reads of one run come back in the order they were logged, and no other run's.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Reads_come_back_in_order_per_run()
    {
        var store = CreateStore();

        _ = await store.Log(Instance, "1.1", new GraphRead("feature:a", "context_node", false, null, _at), None);
        _ = await store.Log(Instance, "1.2", new GraphRead("feature:b", "context_query", false, null, _at), None);
        _ = await store.Log(Instance, "1.1", new GraphRead("feature:z", "context_neighbors", true, "why", _at.AddMinutes(1)), None);
        var first = (await store.Reads(Instance, "1.1", None)).Value;
        var none = (await store.Reads(Instance, "9.9", None)).Value;

        Assert.Equal(["feature:a", "feature:z"], first.Select(r => r.Node));
        Assert.Equal((true, "why"), (first[1].Outside, first[1].Reason));
        Assert.Empty(none);
    }
}
