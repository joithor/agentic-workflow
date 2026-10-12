using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Aw.Kernel.Data;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

/// <summary>What a failed, cancelled or crashed save leaves behind, and the upgrade of an S-1A store (integration, a temp home).</summary>
public sealed class GraphStoreFailureTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly ContextStoreSettings _settings;
    private readonly ContextDatabase _database;

    public GraphStoreFailureTests()
    {
        _settings = SettingsFromEnvironment.Read(_home.Environment);
        _database = new ContextDatabase(_settings, new FakeTimeProvider());
    }

    public void Dispose()
    {
        _database.Dispose();
        _home.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task A_failed_save_leaves_no_version_and_the_current_one_stays_current()
    {
        var store = new SqliteGraphStore(_database, _settings);
        var first = (await store.SaveCurrent(SampleMap.Build(), SampleMap.At, Ct)).Value;
        var map = SampleMap.Build(" (2)");
        var broken = map with { Graph = map.Graph with { Edges = [.. map.Graph.Edges, map.Graph.Edges[0]] } };   // a duplicate edge: the UNIQUE index refuses it mid-write

        var saved = await store.SaveCurrent(broken, SampleMap.At, Ct);

        Assert.Equal("SND-CONTEXT-004", saved.Error.Code);
        Assert.Equal(first.Number, Assert.Single((await store.Versions(Ct)).Value).Number);
        Assert.Equal(1L, await Count("SELECT count(*) FROM map_version"));
        Assert.Equal(Canonical.Of(SampleMap.Build().Graph), Canonical.Of((await store.Read(first.Number, Ct)).Value));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_building_version_left_by_a_crash_is_swept_by_the_next_save()
    {
        var store = new SqliteGraphStore(_database, _settings);
        _ = await store.SaveCurrent(SampleMap.Build(), SampleMap.At, Ct);
        await Execute("INSERT INTO map_version (repo, key_version, created, status) VALUES ('aw-split-panes', 1, '2026-07-15T10:00:00Z', 'building')");

        _ = await store.SaveCurrent(SampleMap.Build(" (2)"), SampleMap.At, Ct);

        Assert.Equal(0L, await Count("SELECT count(*) FROM map_version WHERE status = 'building'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_S1A_store_upgrades_to_the_graph_tables_and_keeps_its_rows()
    {
        var connections = new SqliteConnectionFactory(new SqliteOptions(_settings.DatabasePath));
        Directory.CreateDirectory(_settings.Home);
        Assert.True((await new MigrationRunner(connections, new FakeTimeProvider()).Migrate(ContextDatabase.Migrations, Ct, targetVersion: 1)).IsSuccess);
        await Execute("INSERT INTO source_cursor (source, repo, cursor) VALUES ('repo-docs', 'aw-split-panes', 'a41b07d')", migrate: false);
        var store = new SqliteGraphStore(_database, _settings);

        Assert.Null((await store.Current(Ct)).Value);   // a 0001 store has no map, and the read didn't migrate it
        Assert.Equal(0L, await Count("SELECT count(*) FROM sqlite_master WHERE name = 'map_version'", migrate: false));
        var saved = await store.SaveCurrent(SampleMap.Build(), SampleMap.At, Ct);

        Assert.True(saved.IsSuccess);
        Assert.Equal(1L, await Count("SELECT count(*) FROM source_cursor WHERE cursor = 'a41b07d'"));
        Assert.True(File.Exists(_settings.DatabasePath + ".bak-v1"));
    }

    private async Task<long> Count(string sql, bool migrate = true)
    {
        var sessions = migrate ? (await _database.Writable(Ct)).Value : _database.Sessions;
        await using var read = (await sessions.BeginRead(Ct)).Value;
        return (await SqliteQueries.One(read, new SqlStatement(sql), new LongMapper(), Ct)).Value;
    }

    private async Task Execute(string sql, bool migrate = true)
    {
        var sessions = migrate ? (await _database.Writable(Ct)).Value : _database.Sessions;
        await using var session = (await sessions.BeginSession(Ct)).Value;
        Assert.True((await SqliteQueries.Execute(session, new SqlStatement(sql), Ct)).IsSuccess);
        Assert.True((await session.Commit(Ct)).IsSuccess);
    }

    private sealed class LongMapper : IRowMapper<long>
    {
        public long Map(Microsoft.Data.Sqlite.SqliteDataReader reader) => reader.GetInt64(0);
    }
}
