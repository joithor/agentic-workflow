namespace Aw.Kernel.Testing.Tests;

[Lane(TestLane.Unit)]
public sealed class RealTimeTests
{
    [Fact]
    public void The_clock_reads_follow_the_system_clock()
    {
        var before = TimeProvider.System.GetUtcNow();
        var offset = RealTime.NowOffset;
        var dateTime = RealTime.NowDateTime;
        var after = TimeProvider.System.GetUtcNow();

        Assert.InRange(offset, before, after);
        Assert.InRange(new DateTimeOffset(dateTime, TimeSpan.Zero), before, after);
    }

    [Fact]
    public void Guids_are_fresh() => Assert.NotEqual(RealTime.NewGuid(), RealTime.NewGuid());

    [Fact]
    public void Sleep_blocks_for_at_least_the_duration()
    {
        var start = TimeProvider.System.GetTimestamp();

        RealTime.Sleep(TimeSpan.FromMilliseconds(30));

        Assert.True(TimeProvider.System.GetElapsedTime(start) >= TimeSpan.FromMilliseconds(25));
    }
}
