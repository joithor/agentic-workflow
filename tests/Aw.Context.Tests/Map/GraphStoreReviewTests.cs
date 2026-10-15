using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Aw.Kernel.Data;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

/// <summary>The S-4 migration and what the SQLite store does that the contract can't see (integration, a temp home).</summary>
public sealed class GraphStoreReviewTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly ContextStoreSettings _settings;
    private readonly ContextDatabase _database;

    public GraphStoreReviewTests()
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
    public async Task A_0003_store_upgrades_keeping_its_overrides_and_gains_propose_and_undo()
    {
        var connections = new SqliteConnectionFactory(new SqliteOptions(_settings.DatabasePath));
        _ = Directory.CreateDirectory(_settings.Home);
        Assert.True((await new MigrationRunner(connections, new FakeTimeProvider()).Migrate(ContextDatabase.Migrations, Ct, targetVersion: 3)).IsSuccess);
        await Execute("INSERT INTO map_override (id, action, target, label, other, who, at, rejection) VALUES ('ovr_0123456789abcdef', 'rename', 'feature:a.b', 'New', NULL, 'avery', '2026-07-15T10:00:00Z', NULL)", migrate: false);
        var store = new SqliteGraphStore(_database, _settings);

        var overrides = (await store.Overrides(Ct)).Value;   // a read on a 0003 store never migrates it and still reads the old columns

        Assert.Equal([OverrideAction.Rename], overrides.Select(o => o.Action));
        Assert.Null(overrides[0].Link);
        var added = await store.AppendOverrides([new MapOverride(OverrideAction.Undo, NodeKey.From("feature:a.b"), "avery", SampleMap.At, Undoes: OverrideId.From("ovr_0123456789abcdef"))], Ct);

        Assert.Equal(1, added.Value);
        Assert.Equal(2, (await store.Overrides(Ct)).Value.Count);
        Assert.Equal(1L, await Count("SELECT count(*) FROM map_override WHERE id = 'ovr_0123456789abcdef' AND label = 'New' AND link IS NULL AND payload IS NULL"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_action_outside_the_list_is_still_refused_by_the_table()
    {
        _ = await new SqliteGraphStore(_database, _settings).SaveCurrent(SampleMap.Build(), SampleMap.At, Ct);

        var bad = await TryExecute("INSERT INTO map_override (id, action, target, who, at) VALUES ('x', 'explode', 'feature:a.b', 'avery', '2026-07-15T10:00:00Z')");

        Assert.False(bad);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_building_version_left_by_a_crash_is_swept_by_the_next_draft()
    {
        var store = new SqliteGraphStore(_database, _settings);
        _ = await store.SaveDraft(SampleMap.Build(), SampleMap.At, Ct);
        await Execute("INSERT INTO map_version (repo, key_version, created, status) VALUES ('aw-split-panes', 1, '2026-07-15T10:00:00Z', 'building')");

        _ = await store.SaveDraft(SampleMap.Build(" (2)"), SampleMap.At, Ct);

        Assert.Equal(0L, await Count("SELECT count(*) FROM map_version WHERE status = 'building'"));
        Assert.Equal(1L, await Count("SELECT count(*) FROM map_version WHERE status = 'draft'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_failed_draft_keeps_the_open_draft()
    {
        var store = new SqliteGraphStore(_database, _settings);
        var open = (await store.SaveDraft(SampleMap.Build(), SampleMap.At, Ct)).Value;
        var map = SampleMap.Build(" (2)");
        var broken = map with { Graph = map.Graph with { Edges = [.. map.Graph.Edges, map.Graph.Edges[0]] } };

        var saved = await store.SaveDraft(broken, SampleMap.At, Ct);

        Assert.Equal("SND-CONTEXT-004", saved.Error.Code);
        Assert.Equal(open.Number, Assert.Single((await store.Versions(Ct)).Value).Number);
        Assert.Equal(Canonical.Of(SampleMap.Build().Graph), Canonical.Of((await store.Read(open.Number, Ct)).Value));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_copied_draft_keeps_edge_evidence_and_the_current_version_has_one_current_row()
    {
        var store = new SqliteGraphStore(_database, _settings);
        _ = await store.SaveCurrent(SampleMap.Build(), SampleMap.At, Ct);
        var draft = (await store.OpenDraft(SampleMap.At, Ct)).Value;
        var promoted = await store.Promote(draft.Number, Ct);

        Assert.True(promoted.IsSuccess);
        Assert.Equal(1L, await Count("SELECT count(*) FROM map_version WHERE status = 'current'"));
        Assert.Equal(await Count("SELECT count(*) FROM edge_evidence WHERE edge_id IN (SELECT id FROM edge WHERE map_version = 1)"),
            await Count(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"SELECT count(*) FROM edge_evidence WHERE edge_id IN (SELECT id FROM edge WHERE map_version = {draft.Number})")));
    }

    private async Task<long> Count(string sql, bool migrate = true)
    {
        var sessions = migrate ? (await _database.Writable(Ct)).Value : _database.Sessions;
        await using var read = (await sessions.BeginRead(Ct)).Value;
        return (await SqliteQueries.One(read, new SqlStatement(sql), new LongMapper(), Ct)).Value;
    }

    private async Task Execute(string sql, bool migrate = true) => Assert.True(await TryExecute(sql, migrate));

    private async Task<bool> TryExecute(string sql, bool migrate = true)
    {
        var sessions = migrate ? (await _database.Writable(Ct)).Value : _database.Sessions;
        await using var session = (await sessions.BeginSession(Ct)).Value;
        if ((await SqliteQueries.Execute(session, new SqlStatement(sql), Ct)).IsError)
        {
            return false;
        }

        return (await session.Commit(Ct)).IsSuccess;
    }

    private sealed class LongMapper : IRowMapper<long>
    {
        public long Map(Microsoft.Data.Sqlite.SqliteDataReader reader) => reader.GetInt64(0);
    }
}
