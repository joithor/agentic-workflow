using Aw.Kernel.Testing;

namespace Aw.Kernel.Data.Tests;

/// <summary>The shared read-repository contract bound to SqliteReadRepository (through ItemRepository) on a temp file (integration lane).</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteReadRepositoryContractTests : ReadRepositoryContract<Item, ItemId>, IAsyncDisposable
{
    private readonly TempDatabase _db = new();
    private SqliteUnitOfWork? _work;

    public async ValueTask DisposeAsync()
    {
        if (_work is not null)
        {
            await _work.DisposeAsync();
        }

        _db.Dispose();
    }

    protected override async ValueTask Seed(Item entity, CancellationToken cancellationToken) =>
        Assert.True((await (await Items(cancellationToken)).Add(entity, cancellationToken)).IsSuccess);

    protected override async ValueTask<Result<Item>> Get(ItemId id, CancellationToken cancellationToken) =>
        await (await Items(cancellationToken)).Get(id, cancellationToken);

    protected override async ValueTask<bool> Exists(ItemId id, CancellationToken cancellationToken) =>
        (await (await Items(cancellationToken)).Exists(id, cancellationToken)).Value;

    protected override ItemId IdOf(Item entity) => entity.Id;

    protected override Item NewEntity(int seed) =>
        new(new ItemId(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"item-{seed}")), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"name-{seed}"), seed);

    protected override ItemId MissingId() => new("missing");

    private async ValueTask<ItemRepository> Items(CancellationToken cancellationToken)
    {
        if (_work is null)
        {
            await Schema.CreateItems(_db);
            _work = (await new SqliteUnitOfWorkFactory(_db.Factory()).BeginSession(cancellationToken)).Value;
        }

        return new ItemRepository(_work);
    }
}
