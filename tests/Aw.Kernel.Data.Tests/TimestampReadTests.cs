namespace Aw.Kernel.Data.Tests;

[Trait("Lane", "integration")]
public sealed class TimestampReadTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("2026-10-10T07:05:07.0420000Z", true)]
    [InlineData("2026-10-10T07:05:07Z", false)]
    [InlineData("2026-10-10T07:05:07.0420000+00:00", false)]
    [InlineData("not a time", false)]
    public async Task GetTimestamp_reads_only_utc_round_trip_text(string stored, bool readable)
    {
        using var db = new TempDatabase();
        await using var connection = (await db.Factory().Open(Ct)).Value;
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT $value";
        _ = command.Bind("$value", stored);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));

        if (readable)
        {
            Assert.Equal(new DateTimeOffset(2026, 10, 10, 7, 5, 7, 42, TimeSpan.Zero), SqliteRead.GetTimestamp(reader, 0));
        }
        else
        {
            _ = Assert.Throws<InvalidDataException>(() => SqliteRead.GetTimestamp(reader, 0));
        }
    }
}
