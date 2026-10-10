namespace Aw.Kernel.Testing.Tests;

[Lane(TestLane.Unit)]
public sealed class LaneTests
{
    [Theory]
    [InlineData(TestLane.Unit, "unit")]
    [InlineData(TestLane.Integration, "integration")]
    [InlineData(TestLane.Heavy, "heavy")]
    public void Each_lane_has_its_trait_value(TestLane lane, string expected) =>
        Assert.Equal(expected, Lanes.NameOf(lane));

    [Fact]
    public void Lane_attribute_emits_one_lane_trait()
    {
        var traits = new LaneAttribute(TestLane.Integration).GetTraits();

        var trait = Assert.Single(traits);
        Assert.Equal(new KeyValuePair<string, string>("Lane", "integration"), trait);
    }

    [Fact]
    public void Budgets_follow_the_spec_lanes()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), Lanes.BudgetOf(TestLane.Unit));
        Assert.Equal(TimeSpan.FromSeconds(60), Lanes.BudgetOf(TestLane.Integration));
        Assert.Equal(Timeout.InfiniteTimeSpan, Lanes.BudgetOf(TestLane.Heavy));
    }

    [Fact]
    public void An_undefined_lane_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Lanes.NameOf((TestLane)99));
}
