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
    [InlineData(7, 0, true)]
    [InlineData(8, 0, false)]    // exactly at the floor is not low
    [InlineData(16, 8, false)]   // swap 100% full with RAM to spare is not low
    [InlineData(16, 7, false)]
    [InlineData(7, 8, true)]     // RAM under the floor is low whatever swap is
    public void Memory_is_low_only_when_available_ram_is_under_the_floor(long memGb, long swapUsedGb, bool low) =>
        Assert.Equal(low, ResourceRules.MemoryLow(Host(memGb: memGb, swapUsedGb: swapUsedGb), _limits));

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
    [InlineData(78, 4, null)]    // 30 + 3 lanes x 4 x 4 = 78
    [InlineData(77, 3, BuilderAdvice.DiskLimit)]    // 30 + 3 x 3 x 4 = 66 fits, 78 does not
    [InlineData(65, 2, BuilderAdvice.DiskLimit)]    // 66 does not fit, 30 + 3 x 2 x 4 = 54 does
    [InlineData(53, 1, BuilderAdvice.DiskLimit)]    // 54 does not fit; one builder is the least
    [InlineData(10, 1, BuilderAdvice.DiskLimit)]    // far below the floor: still one
    public void Builders_shrink_while_the_disk_cannot_hold_the_floor_and_every_lanes_worktrees(long diskGb, int perLane, string? limit)
    {
        var advice = BuilderAdvice.Compute(Host(diskGb: diskGb), _limits, new BuildersConfig(MaxPerLane: 4, WorktreeGb: 4), lanes: 3);

        Assert.Equal(perLane, advice.PerLane);
        Assert.Equal(limit, advice.Limit);
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

        Assert.Equal((1, BuilderAdvice.MemoryLimit, "builders: 1 per lane (memory low)"), (advice.PerLane, advice.Limit, advice.Line()));
    }

    [Fact, Trait("Lane", "unit")]
    public void Full_swap_with_ram_to_spare_keeps_the_configured_builders()
    {
        var advice = BuilderAdvice.Compute(Host(diskGb: 900, memGb: 14, swapUsedGb: 8), _limits, new BuildersConfig(MaxPerLane: 4), lanes: 2);

        Assert.Equal((4, null), (advice.PerLane, advice.Limit));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_maximum_below_one_or_no_lanes_still_gives_at_least_one_builder()
    {
        Assert.Equal(1, BuilderAdvice.Compute(Host(), _limits, new BuildersConfig(MaxPerLane: 0), lanes: 0).PerLane);
        Assert.Equal(4, BuilderAdvice.Compute(Host(diskGb: 100), _limits, new BuildersConfig(MaxPerLane: 4), lanes: 0).PerLane);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(132, 8)]    // (132 - 30) / (3 lanes x 4) = 8: no cap, the disk is the only limit
    [InlineData(78, 4)]
    [InlineData(53, 1)]
    public void With_no_configured_maximum_the_disk_alone_sets_the_builders(long diskGb, int perLane)
    {
        var advice = BuilderAdvice.Compute(Host(diskGb: diskGb), _limits, new BuildersConfig(), lanes: 3);

        Assert.Equal((perLane, null), (advice.PerLane, advice.MaxPerLane));
    }

    [Fact, Trait("Lane", "unit")]
    public void With_no_maximum_and_worktrees_that_take_no_disk_nothing_limits_the_builders()
    {
        var advice = BuilderAdvice.Compute(Host(), _limits, new BuildersConfig(WorktreeGb: 0), lanes: 3);

        Assert.Equal((int.MaxValue, null, "builders: no limit per lane (disk 100 GB free, mem 16 GB available)"), (advice.PerLane, advice.Limit, advice.Line()));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_builders_default_sets_no_maximum() =>
        Assert.Null(new BuildersConfig().MaxPerLane);
}
