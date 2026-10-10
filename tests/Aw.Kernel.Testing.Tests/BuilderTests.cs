namespace Aw.Kernel.Testing.Tests;

[Lane(TestLane.Unit)]
public sealed class BuilderTests
{
    [Fact]
    public void Defaults_build_a_valid_value()
    {
        var item = A.Item().Build();

        Assert.Equal(new Item(ItemId.From(1), "item-1", Size.S), item);
    }

    [Fact]
    public void Fluent_changes_override_defaults() =>
        Assert.Equal(Size.L, A.Item().WithSize(Size.L).Build().Size);

    [Fact]
    public void With_applies_a_one_off_change() =>
        Assert.Equal("renamed", A.Item().With(b => b.Title = "renamed").Build().Title);

    [Fact]
    public void BuildMany_varies_each_value_by_index()
    {
        var items = A.Item().BuildMany(3, (b, i) => b.WithSeed(i + 1));

        Assert.Equal([ItemId.From(1), ItemId.From(2), ItemId.From(3)], items.Select(i => i.Id));
    }

    [Fact]
    public void BuildMany_refuses_a_negative_count() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => A.Item().BuildMany(-1, (_, _) => { }));
}
