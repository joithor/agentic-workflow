namespace Aw.Kernel.Data.Tests;

[Trait("Lane", "integration")]
public sealed class ReadRepositoryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_and_Exists_go_through_the_generic_port()
    {
        using var db = new TempDatabase();
        await Schema.CreateItems(db);
        await using var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(Ct)).Value;
        var items = new ItemRepository(work);
        _ = await items.Add(new Item(new ItemId("a"), "alpha", 1), Ct);

        var port = Ports.Read(items);

        Assert.Equal("alpha", (await port.Get(new ItemId("a"), Ct)).Value.Name);
        Assert.True((await port.Exists(new ItemId("a"), Ct)).Value);
        Assert.False((await port.Exists(new ItemId("z"), Ct)).Value);
    }

    [Fact]
    public async Task Get_of_a_missing_id_is_not_found_naming_the_type_and_id()
    {
        using var db = new TempDatabase();
        await Schema.CreateItems(db);
        await using var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(Ct)).Value;

        var missing = await new ItemRepository(work).Get(new ItemId("z"), Ct);

        Assert.Equal(DataErrors.NotFound.Code, missing.Error.Code);
        Assert.Contains("Item", missing.Error.Message, StringComparison.Ordinal);
        Assert.Contains("z", missing.Error.Message, StringComparison.Ordinal);
    }
}
