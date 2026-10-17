using Aw.Meta.Core.Config;

namespace Aw.Meta.Tests.Config;

public sealed class ReserveSettingTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(6, 2)]
    [InlineData(7, 3)]
    [InlineData(40, 3)]
    public void The_defaults_reserve_one_slot_from_three_two_from_five_and_all_from_seven(int depth, int reserved) =>
        Assert.Equal(reserved, ReserveSetting.Default.For(depth, cap: 3));

    [Fact, Trait("Lane", "unit")]
    public void All_follows_the_cap_and_a_number_above_the_cap_is_held_to_it()
    {
        var setting = new ReserveSetting(Enabled: true, [new ReserveTier(1, new ReserveSlots(9)), new ReserveTier(2, ReserveSlots.All)]);

        Assert.Equal((2, 5), (setting.For(1, cap: 2), setting.For(2, cap: 5)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_tier_with_the_highest_reached_depth_wins_whatever_the_order()
    {
        var setting = new ReserveSetting(Enabled: true, [new ReserveTier(5, new ReserveSlots(2)), new ReserveTier(2, new ReserveSlots(1))]);

        Assert.Equal((0, 1, 2), (setting.For(1, 3), setting.For(4, 3), setting.For(5, 3)));
    }
}
