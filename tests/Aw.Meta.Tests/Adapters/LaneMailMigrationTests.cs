using Aw.Kernel.Data;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Infrastructure.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.Adapters;

public sealed class LaneMailMigrationTests : IDisposable
{
    private readonly TempDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0003_upgrades_a_v2_database_keeps_its_rows_backs_it_up_and_lane_mail_works()
    {
        var options = MetaDatabase.OptionsFor(_state.Path);
        var clock = new FakeTimeProvider();
        var migrations = EmbeddedMigrations.Load(typeof(MetaDatabase).Assembly, MetaDatabase.ResourcePrefix).Value;
        var connections = new SqliteConnectionFactory(options);
        _ = await new MigrationRunner(connections, clock).Migrate(migrations, CancellationToken.None, targetVersion: 2);
        await using (var connection = (await connections.Open(CancellationToken.None)).Value)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO messages (id, from_socket, from_name, body, at, kind) VALUES ('m1', 's', 'peer', 'old message', '2026-10-15T05:00:00.0000000Z', 'Request')";
            _ = await insert.ExecuteNonQueryAsync(CancellationToken.None);
        }

        var database = new MetaDatabase(options, clock);
        var messages = new SqliteMessageStore(database);
        var mail = new SqliteLaneMail(database);
        var seq = await mail.Send("api", "hello", clock.GetUtcNow(), CancellationToken.None);
        _ = await mail.Deliver([seq.Value], clock.GetUtcNow(), CancellationToken.None);
        var reply = await mail.Reply(new LaneIdentity("t1", "api"), "done", clock.GetUtcNow(), CancellationToken.None);

        var stored = (await messages.Messages(CancellationToken.None)).Value;
        Assert.Equal(["done", "old message"], stored.Select(m => m.Body).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal("lane:api:1", reply.Value.Value);
        Assert.Empty((await mail.Owed("api", CancellationToken.None)).Value);
        await using var check = (await connections.Open(CancellationToken.None)).Value;
        await using var version = check.CreateCommand();
        version.CommandText = "SELECT MAX(version) FROM schema_version";
        Assert.Equal(4L, await version.ExecuteScalarAsync(CancellationToken.None));
        Assert.True(File.Exists(options.DatabasePath + ".bak-v2"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0004_upgrades_a_v3_database_keeps_its_gate_as_untreed_and_stores_a_tree_and_stack()
    {
        var options = MetaDatabase.OptionsFor(_state.Path);
        var clock = new FakeTimeProvider();
        var migrations = EmbeddedMigrations.Load(typeof(MetaDatabase).Assembly, MetaDatabase.ResourcePrefix).Value;
        var connections = new SqliteConnectionFactory(options);
        _ = await new MigrationRunner(connections, clock).Migrate(migrations, CancellationToken.None, targetVersion: 3);
        await using (var connection = (await connections.Open(CancellationToken.None)).Value)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO gates (pr, run, head, main, scopes, verdict, at) VALUES (7, 1, 'old-head', 'old-main', '', 'Pass', '2026-10-15T05:00:00.0000000Z')";
            _ = await insert.ExecuteNonQueryAsync(CancellationToken.None);
        }

        var gates = new SqliteGateStore(new MetaDatabase(options, clock));
        var old = (await gates.Latest(7, CancellationToken.None)).Value!;
        var saved = await gates.Save(new GateRecord(7, "new-head", "m", [], [], GateVerdict.Pass, clock.GetUtcNow()) { Tree = "t1", Stack = [new StackedPr(5, "h5")] }, CancellationToken.None);
        var latest = (await gates.Latest(7, CancellationToken.None)).Value!;

        Assert.Equal("old-head", old.Head);
        Assert.Null(old.Tree);
        Assert.Empty(old.Stack);
        Assert.Equal(2, saved.Value.Run);
        Assert.Equal("t1", latest.Tree);
        Assert.Equal([new StackedPr(5, "h5")], latest.Stack);
        Assert.True(File.Exists(options.DatabasePath + ".bak-v3"));
    }

    public void Dispose() => _state.Dispose();
}
