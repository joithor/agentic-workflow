using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Obligations;

public sealed class ObligationReaderTests
{
    private readonly MetaWorld _world = new();

    private async Task<bool> Rearm(string intervalSeconds, int beatMinutesAgo)
    {
        await _world.Ready();
        var beat = _world.Clock.GetUtcNow().AddMinutes(-beatMinutesAgo).ToString("O", CultureInfo.InvariantCulture);
        _ = await _world.Values.Set(KvKeys.WatchHeartbeat, beat, CancellationToken.None);
        if (intervalSeconds.Length > 0)
        {
            _ = await _world.Values.Set(KvKeys.WatchInterval, intervalSeconds, CancellationToken.None);
        }

        return (await _world.Open()).Any(o => o.Kind == ObligationKind.RearmWatch);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("", 3, false)]     // no interval stored: two default intervals are 4 minutes
    [InlineData("", 5, true)]
    [InlineData("300", 5, false)]   // a 5-minute watch beats every 5 minutes: not stale until 10
    [InlineData("300", 11, true)]
    [InlineData("junk", 5, true)]   // an unreadable interval falls back to the default
    public async Task A_beat_is_stale_after_two_of_the_watch_s_own_intervals(string interval, int minutesAgo, bool stale) =>
        Assert.Equal(stale, await Rearm(interval, minutesAgo));

    [Fact, Trait("Lane", "unit")]
    public async Task No_watch_ever_started_owes_nothing()
    {
        await _world.Ready();

        Assert.DoesNotContain(await _world.Open(), o => o.Kind == ObligationKind.RearmWatch);
    }
}
