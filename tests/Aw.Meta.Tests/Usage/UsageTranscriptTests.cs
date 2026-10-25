using Aw.Meta.Core.Usage;

namespace Aw.Meta.Tests.Usage;

public sealed class UsageTranscriptTests
{
    private static readonly DateTimeOffset _reference = new(2026, 10, 18, 20, 0, 0, TimeSpan.Zero);

    private const string Limit = """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"You've hit your session limit · resets 2:50am (America/Los_Angeles)"}]},"error":"rate_limit","isApiErrorMessage":true,"timestamp":"2026-10-18T19:58:00.000Z"}""";
    private const string Working = """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"Running the tests."}]},"timestamp":"2026-10-18T19:50:00.000Z"}""";
    private const string User = """{"type":"user","message":{"role":"user","content":"continue"},"timestamp":"2026-10-18T19:59:00.000Z"}""";

    private static UsageStop? Read(params string[] lines) => UsageTranscript.Read(lines, _reference, TimeZoneInfo.Utc);

    [Fact, Trait("Lane", "unit")]
    public void A_last_entry_that_is_the_limit_message_is_a_stop()
    {
        var stop = Read(Working, Limit);

        Assert.Equal(UsageKind.Session, stop?.Kind);
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 9, 50, 0, TimeSpan.Zero), stop?.ResetsAt);
    }

    [Fact, Trait("Lane", "unit")]
    public void Entries_that_are_not_turns_do_not_hide_the_stop()
    {
        var stop = Read(Working, Limit, """{"type":"system","subtype":"x"}""", """{"type":"last-prompt","lastPrompt":"go"}""", "not json at all");

        Assert.NotNull(stop);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_turn_after_the_limit_message_means_the_lane_went_on()
    {
        Assert.Null(Read(Limit, User));
        Assert.Null(Read(Limit, Working));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_user_turn_never_counts_even_when_it_quotes_the_message() =>
        Assert.Null(Read(Working, """{"type":"user","message":{"role":"user","content":"You've hit your weekly limit · resets Oct 19, 10pm (America/Los_Angeles)"}}"""));

    [Fact, Trait("Lane", "unit")]
    public void An_assistant_turn_that_only_talks_about_limits_is_not_a_stop() =>
        Assert.Null(Read("""{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"The owner said you've hit your weekly limit, so I will wait."}]}}"""));

    [Fact, Trait("Lane", "unit")]
    public void A_transcript_with_no_limit_or_no_turns_has_no_stop()
    {
        Assert.Null(Read(Working));
        Assert.Null(Read());
        Assert.Null(Read("""{"type":"system"}"""));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_entry_time_not_the_file_time_is_when_a_bare_clock_time_counts_from()
    {
        // The entry was written at 10:00Z; the file was last touched at 20:00Z. "1pm" counts from the entry: 13:00Z the same day.
        var stop = Read("""{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"You've hit your session limit · resets 1pm (UTC)"}]},"timestamp":"2026-10-18T10:00:00.000Z"}""");

        Assert.Equal(new DateTimeOffset(2026, 10, 18, 13, 0, 0, TimeSpan.Zero), stop?.ResetsAt);
    }
}
