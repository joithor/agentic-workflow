namespace Aw.Kernel.Data.Tests;

[Trait("Lane", "integration")]
public sealed class SqliteRuntimeTests
{
    [Fact]
    public void The_os_library_is_new_enough_and_has_json()
    {
        var check = SqliteRuntime.Check();

        Assert.True(check.IsSuccess);
        Assert.Equal(SqliteRuntime.Version, check.Value);
        Assert.True(Version.Parse(check.Value) >= Version.Parse(SqliteRuntime.MinimumVersion));
    }

    [Fact]
    public void An_old_library_is_unavailable_naming_the_minimum_and_the_fallback()
    {
        var old = SqliteRuntime.Evaluate(3_031_001, "3.31.1", json: true);

        Assert.Equal(DataErrors.Unavailable.Code, old.Error.Code);
        Assert.Contains("3.38.0", old.Error.Message, StringComparison.Ordinal);
        Assert.Contains("e_sqlite3", old.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_library_without_json_is_unavailable() =>
        Assert.Equal(DataErrors.Unavailable.Code, SqliteRuntime.Evaluate(3_045_000, "3.45.0", json: false).Error.Code);

    [Fact]
    public async Task Choosing_a_provider_after_the_first_connection_is_a_bug()
    {
        using var db = new TempDatabase();
        await using var connection = (await db.Factory().Open(TestContext.Current.CancellationToken)).Value;

        _ = Assert.Throws<InvalidOperationException>(() => SqliteRuntime.Initialize(new SQLitePCL.SQLite3Provider_sqlite3()));
    }
}
