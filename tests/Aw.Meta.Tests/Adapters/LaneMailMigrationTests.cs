using Aw.Kernel.Data;
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

    public void Dispose() => _state.Dispose();
}
