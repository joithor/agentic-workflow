using Microsoft.Extensions.Time.Testing;

namespace Aw.Kernel.Testing.Tests;

[Lane(TestLane.Unit)]
public sealed class KnownInstantsTests
{
    [Fact]
    public void A_fake_clock_starts_at_the_epoch()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);

        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(KnownInstants.Parse("2026-01-01T00:05:00Z"), clock.GetUtcNow());
    }

    [Fact]
    public void Parse_reads_milliseconds() =>
        Assert.Equal(
            new DateTimeOffset(2026, 10, 10, 6, 0, 0, 123, TimeSpan.Zero),
            KnownInstants.Parse("2026-10-10T06:00:00.123Z"));

    [Theory]
    [InlineData("2026-10-10T06:00:00+02:00")]
    [InlineData("2026-10-10 06:00:00Z")]
    [InlineData("yesterday")]
    public void Parse_refuses_anything_but_utc_iso(string text) =>
        Assert.Throws<FormatException>(() => KnownInstants.Parse(text));
}
