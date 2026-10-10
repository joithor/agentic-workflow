using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data.Tests;

[Trait("Lane", "integration")]
public sealed class QueryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<SqliteUnitOfWork> Seeded(TempDatabase db)
    {
        await Schema.CreateItems(db);
        var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(Ct)).Value;
        var items = new ItemRepository(work);
        Assert.True((await items.Add(new Item(new ItemId("b"), "beta", 2), Ct)).IsSuccess);
        Assert.True((await items.Add(new Item(new ItemId("a"), "alpha", 1), Ct)).IsSuccess);
        return work;
    }

    [Fact]
    public async Task Single_returns_the_mapped_row()
    {
        using var db = new TempDatabase();
        await using var work = await Seeded(db);

        var item = await SqliteQueries.Single(work, new SqlStatement("SELECT id, name, size FROM items WHERE name = $n", c => c.Bind("$n", "beta")), ItemMapper.Instance, Ct);

        Assert.Equal(new Item(new ItemId("b"), "beta", 2), item.Value);
    }

    [Fact]
    public async Task Single_with_no_row_is_not_found_and_names_the_subject()
    {
        using var db = new TempDatabase();
        await using var work = await Seeded(db);

        var item = await SqliteQueries.Single(work, new SqlStatement("SELECT id, name, size FROM items WHERE name = $n", c => c.Bind("$n", "gamma"), "item named gamma"), ItemMapper.Instance, Ct);

        Assert.Equal(DataErrors.NotFound.Code, item.Error.Code);
        Assert.Equal(ErrorKind.NotFound, item.Error.Kind);
        Assert.Contains("item named gamma", item.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_returns_every_row_in_query_order()
    {
        using var db = new TempDatabase();
        await using var work = await Seeded(db);

        var items = await SqliteQueries.List(work, new SqlStatement("SELECT id, name, size FROM items ORDER BY id"), ItemMapper.Instance, Ct);

        Assert.Equal(["alpha", "beta"], items.Value.Select(i => i.Name));
    }

    [Fact]
    public async Task Execute_that_breaks_a_constraint_is_a_conflict()
    {
        using var db = new TempDatabase();
        await using var work = await Seeded(db);

        var added = await new ItemRepository(work).Add(new Item(new ItemId("c"), "alpha", 3), Ct);

        Assert.Equal(DataErrors.Constraint.Code, added.Error.Code);
        Assert.Equal(ErrorKind.Conflict, added.Error.Kind);
    }

    [Fact]
    public async Task Exists_reads_the_first_column_as_a_boolean()
    {
        using var db = new TempDatabase();
        await using var work = await Seeded(db);

        var yes = await SqliteQueries.Exists(work, new SqlStatement("SELECT EXISTS (SELECT 1 FROM items WHERE id = 'a')"), Ct);
        var no = await SqliteQueries.Exists(work, new SqlStatement("SELECT EXISTS (SELECT 1 FROM items WHERE id = 'z')"), Ct);

        Assert.True(yes.Value);
        Assert.False(no.Value);
    }

    [Fact]
    public async Task Broken_sql_is_a_bug_and_throws()
    {
        using var db = new TempDatabase();
        await using var work = await Seeded(db);

        await Assert.ThrowsAsync<SqliteException>(async () =>
            await SqliteQueries.Execute(work, new SqlStatement("SELEC nonsense"), Ct));
    }
}
