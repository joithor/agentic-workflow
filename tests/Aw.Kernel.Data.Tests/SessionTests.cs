namespace Aw.Kernel.Data.Tests;

[Trait("Lane", "integration")]
public sealed class SessionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_file_that_is_not_a_database_is_damaged_not_unavailable()
    {
        using var db = new TempDatabase();
        await File.WriteAllTextAsync(db.Path, new string('x', 4096), Ct);

        var opened = await db.Factory().Open(Ct);

        Assert.Equal(DataErrors.Damaged.Code, opened.Error.Code);
    }

    [Fact]
    public async Task A_database_that_cannot_use_wal_is_refused()
    {
        var opened = await new SqliteConnectionFactory(new SqliteOptions(":memory:")).Open(Ct);

        Assert.Equal(DataErrors.Unavailable.Code, opened.Error.Code);
        Assert.Contains("WAL", opened.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reading_a_missing_database_is_unavailable_and_creates_no_file()
    {
        using var db = new TempDatabase();

        var read = await new SqliteUnitOfWorkFactory(db.Factory()).BeginRead(Ct);

        Assert.Equal(DataErrors.Unavailable.Code, read.Error.Code);
        Assert.Empty(Directory.GetFiles(db.Directory));
    }

    [Fact]
    public async Task A_read_session_reads_while_a_writer_holds_the_write_lock_and_cannot_write()
    {
        using var db = new TempDatabase();
        await Schema.CreateItems(db);
        var sessions = new SqliteUnitOfWorkFactory(db.Factory(TimeSpan.FromMilliseconds(200)));
        await using var writer = (await sessions.BeginSession(Ct)).Value;
        _ = await new ItemRepository(writer).Add(new Item(new ItemId("w"), "uncommitted", 1), Ct);

        await using var reader = (await sessions.BeginRead(Ct)).Value;
        var rows = await SqliteQueries.List(reader, new SqlStatement("SELECT id, name, size FROM items"), ItemMapper.Instance, Ct);

        Assert.Empty(rows.Value);
        var write = await SqliteQueries.Execute(reader, new SqlStatement("INSERT INTO items VALUES ('r', 'r', 1)"), Ct);
        Assert.Equal(DataErrors.Unavailable.Code, write.Error.Code);
    }

    [Fact]
    public async Task Disposing_a_session_releases_its_lock_at_once()
    {
        using var db = new TempDatabase();
        await Schema.CreateItems(db);
        var sessions = new SqliteUnitOfWorkFactory(db.Factory(TimeSpan.FromMilliseconds(50)));

        await using (var work = (await sessions.BeginSession(Ct)).Value)
        {
            _ = await new ItemRepository(work).Add(new Item(new ItemId("a"), "alpha", 1), Ct);
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await using var next = (await sessions.BeginSession(Ct)).Value;
        Assert.True(stopwatch.ElapsedMilliseconds < 500);
    }
}
