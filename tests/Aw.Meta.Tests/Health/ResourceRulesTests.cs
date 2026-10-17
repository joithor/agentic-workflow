using Aw.Meta.Core.Config;
using Aw.Meta.Core.Health;

namespace Aw.Meta.Tests.Health;

public sealed class ResourceRulesTests
{
    private const long Gb = 1024L * 1024 * 1024;
    private const long GbInKb = 1024L * 1024;
    private static readonly ResourcesConfig _limits = new(DiskFloorGb: 30, MemFloorGb: 8, SwapHighPct: 90);

    private static HostResources Host(long diskGb = 100, long memGb = 16, long swapUsedGb = 0, long swapTotalGb = 8) =>
        new(MemTotalKb: 64 * GbInKb, MemAvailableKb: memGb * GbInKb, SwapUsedKb: swapUsedGb * GbInKb, DiskFreeBytes: diskGb * Gb, SwapTotalKb: swapTotalGb * GbInKb);

    [Theory, Trait("Lane", "unit")]
    [InlineData(29, true)]
    [InlineData(30, false)]
    [InlineData(500, false)]
    public void Disk_is_low_below_the_floor(long diskGb, bool low) =>
        Assert.Equal(low, ResourceRules.DiskLow(Host(diskGb: diskGb), _limits));

    [Theory, Trait("Lane", "unit")]
    [InlineData(7, 0, false, true)]
    [InlineData(8, 0, false, false)]
    [InlineData(16, 8, true, false)]   // 8 of 8 GB of swap is 100%
    [InlineData(16, 7, false, false)]  // 7 of 8 GB is 87%
    public void Memory_is_low_below_the_floor_or_when_swap_is_nearly_full(long memGb, long swapUsedGb, bool swapHigh, bool floorLow) =>
        Assert.Equal(floorLow || swapHigh, ResourceRules.MemoryLow(Host(memGb: memGb, swapUsedGb: swapUsedGb), _limits));

    [Fact, Trait("Lane", "unit")]
    public void A_host_with_no_swap_never_trips_the_swap_rule() =>
        Assert.False(ResourceRules.MemoryLow(Host(swapUsedGb: 0, swapTotalGb: 0), _limits));

    [Fact, Trait("Lane", "unit")]
    public void Swap_used_is_a_share_of_the_total() =>
        Assert.Equal((50, 0), (Host(swapUsedGb: 4, swapTotalGb: 8).SwapUsedPercent, Host(swapTotalGb: 0).SwapUsedPercent));

    [Fact, Trait("Lane", "unit")]
    public void With_room_the_configured_maximum_stands_and_the_line_shows_the_figures()
    {
        var advice = BuilderAdvice.Compute(Host(diskGb: 132, memGb: 12), _limits, new BuildersConfig(MaxPerLane: 4, WorktreeGb: 4), lanes: 3);

        Assert.Equal((4, null), (advice.PerLane, advice.Limit));
        Assert.Equal("builders: 4 per lane (disk 132 GB free, mem 12 GB available)", advice.Line());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(78, 4)]    // 30 + 3 lanes x 4 x 4 = 78
    [InlineData(77, 3)]    // 30 + 3 x 3 x 4 = 66 fits, 78 does not
    [InlineData(65, 2)]    // 66 does not fit, 30 + 3 x 2 x 4 = 54 does
    [InlineData(53, 1)]    // 54 does not fit; one builder is the least
    [InlineData(10, 1)]    // far below the floor: still one
    public void Builders_shrink_while_the_disk_cannot_hold_the_floor_and_every_lanes_worktrees(long diskGb, int perLane)
    {
        var advice = BuilderAdvice.Compute(Host(diskGb: diskGb), _limits, new BuildersConfig(MaxPerLane: 4, WorktreeGb: 4), lanes: 3);

        Assert.Equal(perLane, advice.PerLane);
        Assert.Equal(perLane < 4 || diskGb < 30 + (3 * perLane * 4) ? BuilderAdvice.DiskLimit : null, advice.Limit);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reduced_advice_says_why_in_place_of_the_figures() =>
        Assert.Equal(
            "builders: 2 per lane (disk below floor+headroom)",
            BuilderAdvice.Compute(Host(diskGb: 65), _limits, new BuildersConfig(MaxPerLane: 4, WorktreeGb: 4), lanes: 3).Line());

    [Fact, Trait("Lane", "unit")]
    public void Memory_under_its_floor_means_one_builder_whatever_the_disk()
    {
        var advice = BuilderAdvice.Compute(Host(diskGb: 900, memGb: 5), _limits, new BuildersConfig(), lanes: 2);

        Assert.Equal((1, BuilderAdvice.MemoryLimit, "builders: 1 per lane (memory below floor)"), (advice.PerLane, advice.Limit, advice.Line()));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_maximum_below_one_or_no_lanes_still_gives_at_least_one_builder()
    {
        Assert.Equal(1, BuilderAdvice.Compute(Host(), _limits, new BuildersConfig(MaxPerLane: 0), lanes: 0).PerLane);
        Assert.Equal(4, BuilderAdvice.Compute(Host(diskGb: 100), _limits, new BuildersConfig(), lanes: 0).PerLane);
    }
}
