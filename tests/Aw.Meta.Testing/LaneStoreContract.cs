using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The ILaneStore contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class LaneStoreContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract ValueTask<ILaneStore> Create();

    /// <summary>Before anything is saved the map is empty at version zero.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_map_starts_empty()
    {
        var store = await Create();

        var map = await store.Current(_none);

        Expect.That(map.IsSuccess && map.Value.Version == 0 && map.Value.Entries.Count == 0, "empty at version 0");
    }

    /// <summary>A saved map reads back with its order, sources and optional session ids; a later save replaces it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_saved_map_reads_back_and_a_later_save_replaces_it()
    {
        var store = await Create();
        var first = new LaneMapState(1, [A.Assigned("api", "t1-api"), new LaneAssignment("ui", "agentic-9a", SessionId.From("s-9"), LaneSource.Announcement)]);
        var second = new LaneMapState(2, [A.Assigned("engine", "t1-engine")]);

        _ = await store.Save(first, A.At(1), _none);
        var one = await store.Current(_none);
        _ = await store.Save(second, A.At(2), _none);
        var two = await store.Current(_none);

        Expect.That(one.Value.Version == 1 && one.Value.Entries.SequenceEqual(first.Entries), "the first map reads back");
        Expect.That(two.Value.Version == 2 && two.Value.Entries.SequenceEqual(second.Entries), "the second replaces it");
    }
}
