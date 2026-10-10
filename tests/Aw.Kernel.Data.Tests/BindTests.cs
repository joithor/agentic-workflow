namespace Aw.Kernel.Data.Tests;

[Trait("Lane", "integration")]
public sealed class BindTests
{
    private sealed record Row(string? Text, long? Integer, double? Real, bool Flag, byte[]? Blob);

    private sealed class RowMapper : IRowMapper<Row>
    {
        public Row Map(Microsoft.Data.Sqlite.SqliteDataReader reader) => new(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetInt64(1),
            reader.IsDBNull(2) ? null : reader.GetDouble(2),
            reader.GetInt64(3) == 1,
            reader.IsDBNull(4) ? null : (byte[])reader.GetValue(4));
    }

    private static async Task<Row> RoundTrip(Row row)
    {
        using var db = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;
        await using var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(ct)).Value;
        Assert.True((await SqliteQueries.Execute(work, new SqlStatement("CREATE TABLE t (s TEXT, i INTEGER, r REAL, f INTEGER NOT NULL, b BLOB)"), ct)).IsSuccess);
        var insert = new SqlStatement(
            "INSERT INTO t VALUES ($s, $i, $r, $f, $b)",
            c => c.Bind("$s", row.Text).Bind("$i", row.Integer).Bind("$r", row.Real).Bind("$f", row.Flag).Bind("$b", row.Blob));
        Assert.True((await SqliteQueries.Execute(work, insert, ct)).IsSuccess);
        return (await SqliteQueries.One(work, new SqlStatement("SELECT s, i, r, f, b FROM t"), new RowMapper(), ct)).Value;
    }

    [Fact]
    public async Task Every_type_round_trips()
    {
        var back = await RoundTrip(new Row(Text: "it's \"quoted\" ünïcode", Integer: long.MaxValue, Real: 0.1 + 0.2, Flag: true, Blob: [0, 1, 255]));

        Assert.Equal("it's \"quoted\" ünïcode", back.Text);
        Assert.Equal(long.MaxValue, back.Integer);
        Assert.Equal(0.1 + 0.2, back.Real);
        Assert.True(back.Flag);
        Assert.Equal([0, 1, 255], back.Blob);
    }

    [Fact]
    public async Task Nulls_bind_as_sql_null()
    {
        var back = await RoundTrip(new Row(Text: null, Integer: null, Real: null, Flag: false, Blob: null));

        Assert.Equal(new Row(Text: null, Integer: null, Real: null, Flag: false, Blob: null), back);
    }

    private readonly record struct NumberId(long Value);

    [Fact]
    public async Task Typed_ids_bind_through_an_accessor_as_text_or_integer()
    {
        using var db = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;
        await using var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(ct)).Value;
        _ = await SqliteQueries.Execute(work, new SqlStatement("CREATE TABLE ids (t TEXT, i INTEGER)"), ct);

        var insert = new SqlStatement(
            "INSERT INTO ids VALUES ($t, $i)",
            c => c.BindId("$t", new ItemId("a-1"), id => id.Value).BindId("$i", new NumberId(42), id => id.Value));
        _ = await SqliteQueries.Execute(work, insert, ct);

        Assert.True((await SqliteQueries.Exists(work, new SqlStatement("SELECT EXISTS (SELECT 1 FROM ids WHERE t = 'a-1' AND typeof(t) = 'text' AND i = 42 AND typeof(i) = 'integer')"), ct)).Value);
    }

    [Fact]
    public async Task Int_binds_as_integer_and_timestamps_as_utc_iso_text_with_milliseconds()
    {
        using var db = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;
        await using var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(ct)).Value;
        _ = await SqliteQueries.Execute(work, new SqlStatement("CREATE TABLE v (i, t)"), ct);
        var at = new DateTimeOffset(2026, 10, 10, 9, 5, 7, 42, TimeSpan.FromHours(2));

        _ = await SqliteQueries.Execute(work, new SqlStatement("INSERT INTO v VALUES ($i, $t)", c => c.Bind("$i", 7).Bind("$t", at)), ct);

        Assert.True((await SqliteQueries.Exists(work, new SqlStatement("SELECT EXISTS (SELECT 1 FROM v WHERE typeof(i) = 'integer' AND i = 7 AND t = '2026-10-10T07:05:07.042Z')"), ct)).Value);
    }

    private const string Upper = "0F8FAD5B-D9CB-469F-A165-70867728950E";

    private const string FromTs = "7c9e6679-7425-40de-944b-e07fc1f90ae7";

    private readonly record struct RunId(Guid Value);

    private sealed class GuidMapper : IRowMapper<Guid>
    {
        public Guid Map(Microsoft.Data.Sqlite.SqliteDataReader reader) => SqliteRead.GetGuid(reader, 0);
    }

    [Fact]
    public async Task Guids_bind_as_lowercase_text_like_node_randomUUID_and_read_back()
    {
        using var db = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;
        await using var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(ct)).Value;
        _ = await SqliteQueries.Execute(work, new SqlStatement("CREATE TABLE g (id TEXT, run TEXT)"), ct);
        var id = Guid.Parse(Upper, System.Globalization.CultureInfo.InvariantCulture);

        _ = await SqliteQueries.Execute(work, new SqlStatement("INSERT INTO g VALUES ($id, $run)", c => c.Bind("$id", id).BindId("$run", new RunId(id), r => r.Value)), ct);
        // A row the TS build wrote: crypto.randomUUID() is lowercase text.
        _ = await SqliteQueries.Execute(work, new SqlStatement("INSERT INTO g VALUES ('7c9e6679-7425-40de-944b-e07fc1f90ae7', NULL)"), ct);

        Assert.True((await SqliteQueries.Exists(work, new SqlStatement("SELECT EXISTS (SELECT 1 FROM g WHERE id = '0f8fad5b-d9cb-469f-a165-70867728950e' AND run = id AND typeof(id) = 'text')"), ct)).Value);
        var read = await SqliteQueries.List(work, new SqlStatement("SELECT id FROM g ORDER BY rowid"), new GuidMapper(), ct);
        Assert.Equal([id, Guid.Parse(FromTs, System.Globalization.CultureInfo.InvariantCulture)], read.Value);
    }

    [Theory]
    [InlineData("SELECT X'0F8FAD5BD9CB469FA16570867728950E'")]
    [InlineData("SELECT '0F8FAD5B-D9CB-469F-A165-70867728950E'")]
    [InlineData("SELECT '{0f8fad5b-d9cb-469f-a165-70867728950e}'")]
    [InlineData("SELECT 'not a guid'")]
    public async Task GetGuid_refuses_anything_but_lowercase_hyphenated_text(string sql)
    {
        using var db = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;
        await using var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(ct)).Value;

        _ = await Assert.ThrowsAsync<InvalidDataException>(async () => await SqliteQueries.One(work, new SqlStatement(sql), new GuidMapper(), ct));
    }
}
