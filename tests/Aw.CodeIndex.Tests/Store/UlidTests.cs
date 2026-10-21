using Aw.CodeIndex.Infrastructure.Store;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.CodeIndex.Tests.Store;

[Lane(TestLane.Unit)]
public sealed class UlidTests
{
    [Fact]
    public void A_ulid_is_26_characters_of_the_index_file_pattern()
    {
        var ulid = Ulid.New(new FakeTimeProvider(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero)));

        Assert.Matches("^[0-9A-HJKMNP-TV-Z]{26}$", ulid);
        Assert.True(Ulid.IsValid(ulid));
    }

    [Fact]
    public void Ulids_made_at_the_same_time_differ_and_later_ones_sort_after_earlier_ones()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
        var first = Ulid.New(time);
        var sameMillisecond = Ulid.New(time);
        time.Advance(TimeSpan.FromMilliseconds(5));
        var later = Ulid.New(time);

        Assert.NotEqual(first, sameMillisecond, StringComparer.Ordinal);
        Assert.True(string.CompareOrdinal(first, later) < 0);
        Assert.True(string.CompareOrdinal(sameMillisecond, later) < 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("01JC000000000000000000000")]
    [InlineData("01JC00000000000000000000000")]
    [InlineData("01JC0000000000000000000I00")]
    [InlineData("01jc0000000000000000000000")]
    [InlineData("../01JC00000000000000000000")]
    public void A_text_that_is_not_26_crockford_characters_is_not_a_ulid(string text)
        => Assert.False(Ulid.IsValid(text));
}
