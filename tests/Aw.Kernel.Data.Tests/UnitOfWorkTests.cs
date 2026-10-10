namespace Aw.Kernel.Data.Tests;

[Trait("Lane", "integration")]
public sealed class UnitOfWorkTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<long> CountItems(TempDatabase db)
    {
        await using var reader = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(Ct)).Value;
        var rows = await SqliteQueries.List(reader, new SqlStatement("SELECT id, name, size FROM items"), ItemMapper.Instance, Ct);
        return rows.Value.Count;
    }

    [Fact]
    public async Task Commit_makes_the_writes_visible_to_other_connections()
    {
        using var db = new TempDatabase();
        await Schema.CreateItems(db);

        await using (var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(Ct)).Value)
        {
            await new ItemRepository(work).Add(new Item(new ItemId("a"), "alpha", 1), Ct);
            Assert.True((await work.Commit(Ct)).IsSuccess);
        }

        Assert.Equal(1, await CountItems(db));
    }

    [Fact]
    public async Task Disposing_without_a_commit_rolls_back()
    {
        using var db = new TempDatabase();
        await Schema.CreateItems(db);

        await using (var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(Ct)).Value)
        {
            Assert.True((await new ItemRepository(work).Add(new Item(new ItemId("a"), "alpha", 1), Ct)).IsSuccess);
        }

        Assert.Equal(0, await CountItems(db));
    }

    [Fact]
    public async Task Committing_twice_is_a_bug_and_throws()
    {
        using var db = new TempDatabase();
        await using var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(Ct)).Value;
        await work.Commit(Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await work.Commit(Ct));
    }

    [Fact]
    public async Task A_second_writer_waits_for_the_busy_timeout_then_gets_a_timeout_error()
    {
        using var db = new TempDatabase();
        await Schema.CreateItems(db);
        await using var holder = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(Ct)).Value;

        var second = await new SqliteUnitOfWorkFactory(db.Factory(TimeSpan.FromMilliseconds(200))).Begin(Ct);

        Assert.True(second.IsError);
        Assert.Equal(DataErrors.Busy.Code, second.Error.Code);
        Assert.Equal(ErrorKind.Timeout, second.Error.Kind);
    }

    [Fact]
    public async Task The_factory_port_hands_out_a_unit_of_work()
    {
        using var db = new TempDatabase();
        Persistence.IUnitOfWorkFactory port = new SqliteUnitOfWorkFactory(db.Factory());

        var begun = await port.Begin(Ct);

        Assert.True(begun.IsSuccess);
        await using var work = begun.Value;
        Assert.True((await work.Commit(Ct)).IsSuccess);
    }
}
