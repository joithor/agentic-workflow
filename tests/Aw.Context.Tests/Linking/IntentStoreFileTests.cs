using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Intent;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linking;

public sealed class IntentStoreFileTests : IDisposable
{
    private static readonly SourceId _onyx = SourceId.From("onyx");
    private static readonly RecordKey _r103 = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000103");
    private static readonly string[] _suffixes = ["", "-wal", "-shm"];
    private readonly Map.TempHome _home = new();
    private readonly ContextStoreSettings _settings;
    private readonly ContextDatabase _database;

    public IntentStoreFileTests()
    {
        _settings = SettingsFromEnvironment.Read(_home.Environment);
        _database = new ContextDatabase(_settings, new FakeTimeProvider());
    }

    public void Dispose()
    {
        _database.Dispose();
        _home.Dispose();
    }

    private static IntentPage Page() => new(_onyx, [new IntentRecord(_r103, _onyx, "v1", "Toggle split orientation", ["SPL-103"])],
        [new IntentLink(_r103, NodeKey.From("feature:layout.split-pane.orientation"), LinkRule.IssueKey, 1.0, "ab12")], "c1", new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0006_applies_on_a_copy_of_the_0005_schema()
    {
        var factory = new SqliteConnectionFactory(new SqliteOptions(_settings.DatabasePath));
        _ = Directory.CreateDirectory(_settings.Home);
        var runner = new MigrationRunner(factory, new FakeTimeProvider());
        var five = await runner.Migrate([.. ContextDatabase.Migrations.Take(5)], TestContext.Current.CancellationToken);
        Assert.Equal(5, five.Value.ToVersion);

        var all = await runner.Migrate(ContextDatabase.Migrations, TestContext.Current.CancellationToken);

        Assert.Equal(ContextDatabase.Migrations.Count, all.Value.ToVersion);
        Assert.Equal("0006", ContextDatabase.Migrations[5].Version.ToString("D4", System.Globalization.CultureInfo.InvariantCulture));
        await using var connection = new SqliteConnection("Data Source=" + _settings.DatabasePath + ";Mode=ReadOnly");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var tables = connection.CreateCommand();
        tables.CommandText = "SELECT group_concat(name) FROM (SELECT name FROM sqlite_master WHERE type = 'table' AND name LIKE 'intent_%' ORDER BY name)";
        Assert.Equal("intent_conflict,intent_decision,intent_edge,intent_link,intent_record", await tables.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0007_adds_created_at_over_a_0006_database_holding_rows_and_those_rows_read_null()
    {
        var factory = new SqliteConnectionFactory(new SqliteOptions(_settings.DatabasePath));
        _ = Directory.CreateDirectory(_settings.Home);
        var runner = new MigrationRunner(factory, new FakeTimeProvider());
        var six = await runner.Migrate([.. ContextDatabase.Migrations.Take(6)], TestContext.Current.CancellationToken);
        Assert.Equal(6, six.Value.ToVersion);
        await using (var connection = new SqliteConnection("Data Source=" + _settings.DatabasePath))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO intent_record (key, source, version, title, aliases, fetched_at, extraction_state) VALUES ('" + _r103.Value + "', 'onyx', '2026-07-23T09:30:00Z', 'Old row', 'SPL-103', '2026-10-12T09:00:00.0000000Z', 'done')";
            _ = await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var seven = await runner.Migrate([.. ContextDatabase.Migrations.Take(7)], TestContext.Current.CancellationToken);

        Assert.Equal(7, seven.Value.ToVersion);
        Assert.Equal("0007", ContextDatabase.Migrations[6].Version.ToString("D4", System.Globalization.CultureInfo.InvariantCulture));
        var record = Assert.Single((await new SqliteIntentStore(_database).Layer(TestContext.Current.CancellationToken)).Value.Records);
        Assert.Equal(("Old row", null), (record.Title, record.CreatedAt));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Intent_rows_survive_promote_and_a_new_draft()
    {
        var store = new SqliteIntentStore(_database);
        var graphs = new SqliteGraphStore(_database, _settings);
        _ = await graphs.SaveCurrent(SampleMap.Build(), SampleMap.At, TestContext.Current.CancellationToken);
        _ = await store.CommitPage(Page(), TestContext.Current.CancellationToken);

        var draft = await graphs.SaveDraft(SampleMap.Build(" (2)"), SampleMap.At, TestContext.Current.CancellationToken);
        _ = await graphs.Promote(draft.Value.Number, TestContext.Current.CancellationToken);
        _ = await graphs.SaveDraft(SampleMap.Build(" (3)"), SampleMap.At, TestContext.Current.CancellationToken);

        var layer = (await store.Layer(TestContext.Current.CancellationToken)).Value;
        _ = Assert.Single(layer.Records);
        _ = Assert.Single(layer.Links);
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task File_modes_stay_0600_with_the_new_tables()
    {
        var store = new SqliteIntentStore(_database);

        _ = await store.CommitPage(Page(), TestContext.Current.CancellationToken);
        _database.RestrictFiles();

        foreach (var file in _suffixes.Select(s => _settings.DatabasePath + s).Where(File.Exists))
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }
    }
}
