using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow.Registry;

namespace Aw.Sindri.Tests.Registry;

/// <summary>Pins compare by their items, so an approval read back from a store equals the one written (engine spec §4.1).</summary>
[Lane(TestLane.Unit)]
public sealed class RecipePinsTests
{
    [Fact]
    public void Pins_with_the_same_items_are_equal()
    {
        var left = new RecipePins([new BlockPin(BlockId.From("step.produce"), RegistryData.Descriptor("v1"))], [new ChildPin(RecipeId.From("workflow.step"), RegistryData.Hash("c"))]);
        var right = new RecipePins([new BlockPin(BlockId.From("step.produce"), RegistryData.Descriptor("v1"))], [new ChildPin(RecipeId.From("workflow.step"), RegistryData.Hash("c"))]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.Equal(RecipePins.None, new RecipePins([], []));
    }

    [Fact]
    public void Pins_with_other_items_or_order_differ()
    {
        var one = new BlockPin(BlockId.From("step.produce"), RegistryData.Descriptor("v1"));
        var two = new BlockPin(BlockId.From("gate.route"), RegistryData.Descriptor("v1"));

        Assert.NotEqual(new RecipePins([one], []), new RecipePins([one with { Hash = RegistryData.Descriptor("v2") }], []));
        Assert.NotEqual(new RecipePins([one, two], []), new RecipePins([two, one], []));
        Assert.NotEqual(new RecipePins([one], []), RecipePins.None);
    }
}
