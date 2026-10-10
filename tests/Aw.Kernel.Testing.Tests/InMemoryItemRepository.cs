
namespace Aw.Kernel.Testing.Tests;

/// <summary>The fake a package's Testing project ships for its port: one per port, checked by the shared contract.</summary>
public sealed class InMemoryItemRepository
{
    private readonly Dictionary<ItemId, Item> _items = [];

    public void Save(Item item) => _items[item.Id] = item;

    public ValueTask<Result<Item>> Get(ItemId id) =>
        ValueTask.FromResult<Result<Item>>(
            _items.TryGetValue(id, out var item) ? item : KernelErrors.NotFound.With($"item {id.Value}"));

    public ValueTask<bool> Exists(ItemId id) => ValueTask.FromResult(_items.ContainsKey(id));
}
