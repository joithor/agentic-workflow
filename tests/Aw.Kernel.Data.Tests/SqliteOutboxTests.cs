using Aw.Kernel.Outbox;

namespace Aw.Kernel.Data.Tests;

/// <summary>What the shared outbox contract can't see: the outbox joins the unit of work, and the delivery state it stores.</summary>
[Trait("Lane", "integration")]
public sealed class SqliteOutboxTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static OutboxCommand Command(int seed) =>
        new(new Guid(a: seed, b: 0, c: 0, d: 0, e: 0, f: 0, g: 0, h: 0, i: 0, j: 0, k: 0), "test.command", "{}", 1, _now,
            "host", "tenant-a", 0, new Guid(a: 7, b: 0, c: 0, d: 0, e: 0, f: 0, g: 0, h: 0, i: 0, j: 0, k: 0), CausationId: null, TraceId: null);

    private static async Task<TempDatabase> Migrated()
    {
        var db = new TempDatabase();
        Assert.True((await new MigrationRunner(db.Factory(), new FixedClock(_now)).Migrate([SqliteOutboxSchema.CreateMigration(1)], Ct)).IsSuccess);
        return db;
    }

    private static async Task<int> PendingCount(TempDatabase db)
    {
        await using var session = new ConnectionSession((await db.Factory().Open(Ct)).Value);
        return (await new SqliteOutbox(session, new FixedClock(_now.AddDays(1)), OutboxRetryPolicy.Default).ReadPending(100, Ct)).Value.Count;
    }

    [Fact]
    public async Task A_command_enqueued_in_a_unit_of_work_commits_with_it()
    {
        using var db = await Migrated();
        await using (var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(Ct)).Value)
        {
            Assert.True((await new SqliteOutbox(work, new FixedClock(_now), OutboxRetryPolicy.Default).Enqueue(Command(1), Ct)).IsSuccess);
            Assert.Equal(0, await PendingCount(db));
            Assert.True((await work.Commit(Ct)).IsSuccess);
        }

        Assert.Equal(1, await PendingCount(db));
    }

    [Fact]
    public async Task A_command_enqueued_in_a_rolled_back_unit_of_work_is_gone()
    {
        using var db = await Migrated();
        await using (var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(Ct)).Value)
        {
            Assert.True((await new SqliteOutbox(work, new FixedClock(_now), OutboxRetryPolicy.Default).Enqueue(Command(1), Ct)).IsSuccess);
        }

        Assert.Equal(0, await PendingCount(db));
    }

    [Fact]
    public async Task Failures_store_the_attempts_the_last_error_and_the_dead_letter_time()
    {
        using var db = await Migrated();
        await using var session = new ConnectionSession((await db.Factory().Open(Ct)).Value);
        var policy = OutboxRetryPolicy.Create(2, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1)).Value;
        var outbox = new SqliteOutbox(session, new FixedClock(_now), policy);
        _ = await outbox.Enqueue(Command(1), Ct);

        _ = await outbox.MarkFailed(Command(1).Id, KernelErrors.Unavailable.With("first"), Ct);
        Assert.Equal(("1", "AW-KERNEL-006", "first", "2026-01-01T00:00:30.0000000Z", null), await Row(session));

        _ = await outbox.MarkFailed(Command(1).Id, KernelErrors.Timeout.With("second"), Ct);
        Assert.Equal(("2", "AW-KERNEL-007", "second", null, "2026-01-01T00:00:00.0000000Z"), await Row(session));
    }

    private static async Task<(string, string?, string?, string?, string?)> Row(ConnectionSession session)
    {
        await using var command = session.Connection.CreateCommand();
        command.CommandText = "SELECT attempts, last_error_code, last_error_message, next_attempt_at, dead_lettered_at FROM outbox_commands";
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        string? Text(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
        return (reader.GetInt64(0).ToString(System.Globalization.CultureInfo.InvariantCulture), Text(1), Text(2), Text(3), Text(4));
    }
}
