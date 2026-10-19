using Aw.Meta.Core.Usage;

namespace Aw.Meta.Tests.Usage;

public sealed class UsageLimitParserTests
{
    // 13:00 in Los Angeles (daylight time, UTC-7) on 2026-10-18.
    private static readonly DateTimeOffset _reference = new(2026, 10, 18, 20, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo _plusTwo = TimeZoneInfo.CreateCustomTimeZone("plus-two", TimeSpan.FromHours(2), "plus-two", "plus-two");

    private static UsageStop? Parse(string text, DateTimeOffset? reference = null) => UsageLimitParser.Parse(text, reference ?? _reference, _plusTwo);

    [Fact, Trait("Lane", "unit")]
    public void A_session_limit_with_a_clock_time_resets_at_its_next_occurrence_in_the_named_zone()
    {
        var stop = Parse("You've hit your session limit · resets 2:50am (America/Los_Angeles)");

        Assert.NotNull(stop);
        Assert.Equal(UsageKind.Session, stop.Kind);
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 9, 50, 0, TimeSpan.Zero), stop.ResetsAt);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_weekly_limit_with_a_date_resets_on_that_date()
    {
        var stop = Parse("You've hit your weekly limit · resets Oct 19, 10pm (America/Los_Angeles)");

        Assert.NotNull(stop);
        Assert.Equal(UsageKind.Weekly, stop.Kind);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 5, 0, 0, TimeSpan.Zero), stop.ResetsAt);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reset_time_with_no_zone_is_in_the_local_zone()
    {
        var stop = Parse("You've hit your session limit · resets 2:50am");

        Assert.Equal(new DateTimeOffset(2026, 10, 19, 0, 50, 0, TimeSpan.Zero), stop?.ResetsAt);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reset_time_that_is_not_in_a_known_format_is_limited_with_the_reset_unknown()
    {
        var stop = Parse("You've hit your session limit · resets when the moon is full");

        Assert.NotNull(stop);
        Assert.Equal(UsageKind.Session, stop.Kind);
        Assert.Null(stop.ResetsAt);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_zone_that_does_not_exist_leaves_the_reset_unknown() =>
        Assert.Null(Parse("You've hit your session limit · resets 2:50am (Mars/Olympus_Mons)")?.ResetsAt);

    [Theory, Trait("Lane", "unit")]
    [InlineData("hello world")]
    [InlineData("the limit of this approach is clear")]
    [InlineData("")]
    public void Text_that_is_not_a_usage_limit_message_is_not_a_stop(string text) =>
        Assert.Null(Parse(text));

    [Theory, Trait("Lane", "unit")]
    [InlineData("You’ve hit your session limit · resets 5pm (UTC)", UsageKind.Session)]
    [InlineData("You've hit your Opus limit · resets 5pm (UTC)", UsageKind.Other)]
    [InlineData("  ⎿  You've hit your weekly limit · resets 5pm (UTC)", UsageKind.Weekly)]
    [InlineData("Claude usage limit reached. Your limit will reset at 5pm (UTC)", UsageKind.Other)]
    public void The_kind_and_the_apostrophe_and_the_prefix_are_tolerated(string text, UsageKind kind)
    {
        var stop = Parse(text);

        Assert.Equal(kind, stop?.Kind);
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 17, 0, 0, TimeSpan.Zero), stop?.ResetsAt);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("resets 12am (UTC)", 19, 0)]
    [InlineData("resets 12pm (UTC)", 19, 12)]
    [InlineData("resets 9:15pm (UTC)", 18, 21)]
    public void Twelve_and_the_afternoon_hours_are_read_correctly(string tail, int day, int hour)
    {
        var stop = Parse("You've hit your session limit · " + tail);

        Assert.Equal(day, stop?.ResetsAt?.Day);
        Assert.Equal(hour, stop?.ResetsAt?.Hour);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_date_already_passed_this_year_means_next_year()
    {
        var stop = Parse("You've hit your weekly limit · resets Jan 2, 3am (UTC)", new DateTimeOffset(2026, 12, 30, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2027, 1, 2, 3, 0, 0, TimeSpan.Zero), stop?.ResetsAt);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pane_time_is_the_occurrence_nearest_to_when_it_was_looked_at_so_it_does_not_slide_to_tomorrow_once_passed()
    {
        const string Text = "You've hit your session limit · resets 5:30am (UTC)";
        var before = UsageLimitParser.Parse(Text, new DateTimeOffset(2026, 10, 15, 5, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, seenNow: true);
        var after = UsageLimitParser.Parse(Text, new DateTimeOffset(2026, 10, 15, 5, 40, 0, TimeSpan.Zero), TimeZoneInfo.Utc, seenNow: true);
        var evening = UsageLimitParser.Parse(Text, new DateTimeOffset(2026, 10, 15, 20, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, seenNow: true);

        Assert.Equal(new DateTimeOffset(2026, 10, 15, 5, 30, 0, TimeSpan.Zero), before?.ResetsAt);
        Assert.Equal(before?.ResetsAt, after?.ResetsAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 16, 5, 30, 0, TimeSpan.Zero), evening?.ResetsAt);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_key_names_the_limit_and_its_reset_so_a_new_limit_has_a_new_key()
    {
        var first = Parse("You've hit your session limit · resets 2:50am (America/Los_Angeles)");
        var again = Parse("  ⎿ You've hit your session limit · resets 2:50am (America/Los_Angeles)");
        var weekly = Parse("You've hit your weekly limit · resets Oct 19, 10pm (America/Los_Angeles)");

        Assert.Equal(first?.Key, again?.Key, StringComparer.Ordinal);
        Assert.NotEqual(first?.Key, weekly?.Key, StringComparer.Ordinal);
        Assert.Equal("Session@2026-10-19T09:50:00", first?.Key);
        Assert.Equal("2026-10-19 09:50:00Z", first?.ResetText);
    }
}
