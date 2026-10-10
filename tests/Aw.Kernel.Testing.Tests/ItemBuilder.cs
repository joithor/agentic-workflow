using System.Globalization;

namespace Aw.Kernel.Testing.Tests;

/// <summary>Builds <see cref="Item"/>s with deterministic defaults: seed 1, size S, title "item-1".</summary>
public sealed class ItemBuilder : Builder<ItemBuilder, Item>
{
    private int _seed = 1;
    private Size _size = Size.S;

    /// <summary>Overrides the title; null keeps the default.</summary>
    public string? Title { get; set; }

    /// <summary>Picks the id and default title.</summary>
    /// <param name="seed">1, 2, 3…</param>
    /// <returns>This builder.</returns>
    public ItemBuilder WithSeed(int seed)
    {
        _seed = seed;
        return this;
    }

    /// <summary>Overrides the size.</summary>
    /// <param name="size">The size.</param>
    /// <returns>This builder.</returns>
    public ItemBuilder WithSize(Size size)
    {
        _size = size;
        return this;
    }

    /// <inheritdoc />
    public override Item Build() =>
        new(ItemId.From(_seed), Title ?? ("item-" + _seed.ToString(CultureInfo.InvariantCulture)), _size);
}
