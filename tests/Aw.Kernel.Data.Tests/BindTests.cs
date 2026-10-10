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
}
