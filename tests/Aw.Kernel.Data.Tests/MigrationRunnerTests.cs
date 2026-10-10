using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data.Tests;

[Trait("Lane", "integration")]
public sealed class MigrationRunnerTests
{
    private static DateTimeOffset Noon => new(2026, 10, 9, 12, 0, 0, 123, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static MigrationRunner Runner(TempDatabase db) => new(db.Factory(), new FixedClock(Noon));

    private static async Task<List<(long Version, string Name, string Checksum, string AppliedAt)>> Applied(TempDatabase db)
    {
        await using var connection = (await db.Factory().Open(Ct)).Value;
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version, name, checksum, applied_at FROM schema_version ORDER BY version";
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var rows = new List<(long, string, string, string)>();
        while (await reader.ReadAsync(Ct))
        {
            rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }

        return rows;
    }

    [Fact]
    public async Task Applies_pending_migrations_in_order_and_records_each_one()
    {
        using var db = new TempDatabase();

        var report = await Runner(db).Migrate([Schema.Items, Schema.Tags], Ct);

        Assert.Equal(new MigrationReport(0, 2, [1, 2]), report.Value);
        var applied = await Applied(db);
        Assert.Equal([1L, 2L], applied.Select(r => r.Version));
        Assert.Equal(["items", "tags"], applied.Select(r => r.Name), StringComparer.Ordinal);
        Assert.All(applied, r => Assert.Equal("2026-10-09T12:00:00.123Z", r.AppliedAt));
        Assert.Equal(Schema.Items.Checksum, applied[0].Checksum);
    }

    [Fact]
    public async Task Running_again_applies_nothing()
    {
        using var db = new TempDatabase();
        _ = await Runner(db).Migrate([Schema.Items, Schema.Tags], Ct);

        var again = await Runner(db).Migrate([Schema.Items, Schema.Tags], Ct);

        Assert.Equal(2, again.Value.FromVersion);
        Assert.Empty(again.Value.Applied);
    }

    [Fact]
    public async Task A_target_version_stops_early_so_each_step_can_be_tested_on_the_previous_schema()
    {
        using var db = new TempDatabase();

        var first = await Runner(db).Migrate([Schema.Items, Schema.Tags, Schema.Notes], Ct, targetVersion: 1);
        var rest = await Runner(db).Migrate([Schema.Items, Schema.Tags, Schema.Notes], Ct);

        Assert.Equal([1], first.Value.Applied);
        Assert.Equal([2, 3], rest.Value.Applied);
    }

    [Theory]
    [InlineData(new[] { 1, 3 })]
    [InlineData(new[] { 2 })]
    [InlineData(new[] { 1, 1 })]
    [InlineData(new[] { 2, 1 })]
    public async Task A_malformed_sequence_is_refused_before_the_database_is_touched(int[] versions)
    {
        using var db = new TempDatabase();
        var migrations = versions.Select(v => new Migration(v, "m" + v.ToString(System.Globalization.CultureInfo.InvariantCulture), "CREATE TABLE t" + v.ToString(System.Globalization.CultureInfo.InvariantCulture) + " (x);")).ToList();

        var refused = await Runner(db).Migrate(migrations, Ct);

        Assert.Equal(DataErrors.MigrationSequence.Code, refused.Error.Code);
        Assert.False(File.Exists(db.Path));
    }

    [Fact]
    public async Task An_applied_migration_whose_text_changed_is_refused_as_drift()
    {
        using var db = new TempDatabase();
        _ = await Runner(db).Migrate([Schema.Items], Ct);

        var edited = Schema.Items with { Sql = Schema.Items.Sql + " -- edited" };
        var refused = await Runner(db).Migrate([edited, Schema.Tags], Ct);

        Assert.Equal(DataErrors.MigrationDrift.Code, refused.Error.Code);
        _ = Assert.Single(await Applied(db));
    }

    [Fact]
    public async Task A_database_newer_than_the_build_is_refused()
    {
        using var db = new TempDatabase();
        _ = await Runner(db).Migrate([Schema.Items, Schema.Tags], Ct);

        var refused = await Runner(db).Migrate([Schema.Items], Ct);

        Assert.Equal(DataErrors.MigrationUnknown.Code, refused.Error.Code);
    }

    [Fact]
    public async Task A_failing_migration_rolls_back_entirely_and_is_not_recorded()
    {
        using var db = new TempDatabase();
        var broken = new Migration(2, "broken", "CREATE TABLE half (x); INSERT INTO nowhere VALUES (1);");

        _ = await Assert.ThrowsAsync<SqliteException>(async () => await Runner(db).Migrate([Schema.Items, broken], Ct));

        Assert.Equal([1L], (await Applied(db)).Select(r => r.Version));
        await using var connection = (await db.Factory().Open(Ct)).Value;
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE name = 'half'";
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync(Ct))!);
    }

    [Fact]
    public void The_checksum_is_sha256_of_the_text_with_lf_line_endings()
    {
        var lf = new Migration(1, "x", "CREATE TABLE x (a);\nCREATE TABLE y (b);\n");
        var crlf = lf with { Sql = lf.Sql.Replace("\n", "\r\n", StringComparison.Ordinal) };

        Assert.Equal(lf.Checksum, crlf.Checksum);
        Assert.Equal(lf.Checksum, (lf with { Sql = lf.Sql.Replace('\n', '\r') }).Checksum);
        Assert.Equal(lf.Checksum, (lf with { Sql = "\uFEFF" + lf.Sql }).Checksum);
        Assert.Equal(
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(lf.Sql))),
            lf.Checksum);
    }
}
