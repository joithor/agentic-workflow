using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Intent;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

/// <summary>
/// A map read builds its view (the deny cut, the overrides applied) once per stored snapshot, overrides, intent layer and caller: every lens and
/// query read rebuilt it, seconds each on a 24k-node map. Counted, not timed.
/// </summary>
public sealed class ReadCacheTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Reads_of_an_unchanged_map_build_its_view_once_and_a_new_override_or_caller_builds_it_again()
    {
        _ = await _world.ImportFixture();
        var builds = 0;
        var reader = new MapReader(_world.Store, _world.Intents, new MapViewCache { Built = () => builds++ });

        var first = await reader.Read(MapWorld.Human, draft: false, MapWorld.None);
        var second = await reader.Read(MapWorld.Human, draft: false, MapWorld.None);
        var third = await reader.Read(MapWorld.Human, draft: false, MapWorld.None);
        var afterReads = builds;
        var node = first.Value.Graph.Nodes[0].Key;
        _ = await _world.Store.AppendOverrides([new MapOverride(OverrideAction.Rename, node, "avery", DateTimeOffset.UnixEpoch, "Renamed")], MapWorld.None);
        var renamed = await reader.Read(MapWorld.Human, draft: false, MapWorld.None);
        var afterOverride = builds;
        _ = await reader.Read(MapWorld.Session(FixtureMap.Repo), draft: false, MapWorld.None);

        Assert.Equal(1, afterReads);
        Assert.Same(first.Value.Graph, third.Value.Graph);
        Assert.Same(second.Value.Graph, third.Value.Graph);
        Assert.Equal(2, afterOverride);
        Assert.Equal("Renamed", renamed.Value.Graph.Resolve(node)?.Label);
        Assert.Equal(3, builds);
    }

    // The SQLite stores hand back the same snapshot and intent layer while no write moved the change stamp, and a fresh one after a write.
    [Fact, Trait("Lane", "integration")]
    public async Task The_sqlite_stores_keep_what_they_loaded_until_a_write_moves_the_change_stamp()
    {
        using var home = new TempHome();
        var settings = SettingsFromEnvironment.Read(home.Environment);
        using var database = new ContextDatabase(settings, new FakeTimeProvider());
        var store = new SqliteGraphStore(database, settings);
        var intents = new SqliteIntentStore(database);
        _ = await store.SaveCurrent(new MapImport(ContextRepoName.From(FixtureMap.Repo), "abc", new GraphSnapshot([new GraphNode(NodeKey.From("feature:tabs"), "Tabs")], [], [], []), []), DateTimeOffset.UnixEpoch, MapWorld.None);

        var first = (await store.Snapshot(draft: false, MapWorld.None)).Value;
        var again = (await store.Snapshot(draft: false, MapWorld.None)).Value;
        var layer = (await intents.Layer(MapWorld.None)).Value;
        var layerAgain = (await intents.Layer(MapWorld.None)).Value;
        await using (var connection = new SqliteConnection("Data Source=" + settings.DatabasePath))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO intent_record (key, source, version, title, aliases, fetched_at, extraction_state) VALUES ('record:onyx/linear/SPL-1', 'onyx', 'v1', 'A record', '', '2026-10-12T09:00:00.0000000Z', 'done')";
            _ = await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var afterSync = (await store.Snapshot(draft: false, MapWorld.None)).Value;
        var layerAfterSync = (await intents.Layer(MapWorld.None)).Value;
        _ = await store.AppendOverrides([new MapOverride(OverrideAction.Rename, NodeKey.From("feature:tabs"), "avery", DateTimeOffset.UnixEpoch, "Tab strip")], MapWorld.None);
        var written = (await store.Snapshot(draft: false, MapWorld.None)).Value;

        Assert.Same(first, again);
        Assert.Same(layer, layerAgain);
        Assert.Same(first, afterSync);   // an intent write moves only the intent layer's counter: the map isn't read again
        Assert.NotSame(layer, layerAfterSync);
        _ = Assert.Single(layerAfterSync.Records);
        Assert.NotSame(first, written);
        _ = Assert.Single(written!.Overrides);
    }
}
