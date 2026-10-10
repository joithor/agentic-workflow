
namespace Aw.Kernel.Testing.Tests;

/// <summary>The contract bound to the fake (unit lane). A package binds the same base to its SQL repository in the integration lane.</summary>
[Lane(TestLane.Unit)]
public sealed class ItemRepositoryContractTests : ReadRepositoryContract<Item, ItemId>
{
    private readonly InMemoryItemRepository _repository = new();

    protected override ValueTask Seed(Item entity, CancellationToken cancellationToken)
    {
        _repository.Save(entity);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<Result<Item>> Get(ItemId id, CancellationToken cancellationToken) => _repository.Get(id);

    protected override ValueTask<bool> Exists(ItemId id, CancellationToken cancellationToken) => _repository.Exists(id);

    protected override ItemId IdOf(Item entity) => entity.Id;

    protected override Item NewEntity(int seed) => A.Item().WithSeed(seed).Build();

    protected override ItemId MissingId() => ItemId.From(999);
}
