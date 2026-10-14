namespace Aw.Redaction.Tests;

[Trait("Lane", "unit")]
public sealed class AdversarialInputTests
{
    [Fact]
    public void A_text_full_of_placeholders_and_secrets_is_scrubbed_not_blocked()
    {
        var key = Shapes.Join("gh", "p_", Shapes.Body, "Ab3dE6gH9");
        var input = string.Concat(Enumerable.Repeat("[REDACTED:github-token] " + key + " ", 2_000));

        var result = Redactor.Default.Scrub(input);

        Assert.Equal(ScrubOutcome.Redacted, result.Outcome);
        Assert.Equal(2_000, result.Hits.Count);
    }

    [Fact]
    public void A_hundred_kilobytes_of_unterminated_begin_lines_is_scrubbed_not_blocked()
    {
        var input = string.Concat(Enumerable.Repeat("-----BEGIN RSA PRIV" + "ATE KEY-----\n", 100_000 / 32));

        var result = Redactor.Default.Scrub(input);

        Assert.NotEqual(ScrubOutcome.Blocked, result.Outcome);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(128 * 1024, 1)]
    [InlineData((128 * 1024) + 1, 2)]
    [InlineData(2 * 128 * 1024, 2)]
    [InlineData((2 * 128 * 1024) + 1, 4)]
    [InlineData(4_000_000, 32)]
    [InlineData(int.MaxValue, 16384)]
    public void The_timeout_doubles_with_each_doubling_of_the_input(int length, int factor)
    {
        var timeout = Redactor.MatchTimeoutFor(TimeSpan.FromMilliseconds(100), length);

        Assert.Equal(TimeSpan.FromMilliseconds(100 * factor), timeout);
    }

    [Fact]
    public void The_timeout_never_shrinks_as_the_input_grows()
    {
        var previous = TimeSpan.Zero;
        for (var length = 0; length <= 64 * 1024 * 1024; length += 997 * 1024)
        {
            var timeout = Redactor.MatchTimeoutFor(TimeSpan.FromMilliseconds(1), length);

            Assert.True(timeout >= previous);
            previous = timeout;
        }
    }

    [Fact]
    public void A_very_long_timeout_is_capped_at_every_size()
    {
        var cap = TimeSpan.FromMilliseconds(int.MaxValue - 1);

        Assert.Equal(cap, Redactor.MatchTimeoutFor(TimeSpan.FromDays(30), 10));
        Assert.Equal(cap, Redactor.MatchTimeoutFor(TimeSpan.FromDays(30), 4_000_000));
        Assert.Equal(cap, Redactor.MatchTimeoutFor(TimeSpan.FromDays(30), int.MaxValue));
    }

    [Fact]
    public void A_scrub_uses_the_timeout_of_its_size_tier()
    {
        var redactor = new Redactor(BuiltinRules.All, TimeSpan.FromMilliseconds(100));

        Assert.Equal(TimeSpan.FromMilliseconds(100), redactor.TimeoutFor(10));
        Assert.Equal(TimeSpan.FromMilliseconds(800), redactor.TimeoutFor(1_000_000));
        Assert.Equal(Redactor.MatchTimeoutFor(TimeSpan.FromMilliseconds(100), 4_000_000), redactor.TimeoutFor(4_000_000));
    }

    [Fact]
    public void A_long_chain_of_token_prefixes_is_scrubbed_not_blocked()
    {
        var input = string.Concat(Enumerable.Repeat("-ey" + "J", 32_000));

        var result = Redactor.Default.Scrub(input);

        Assert.Equal(ScrubOutcome.Clean, result.Outcome);
    }

    [Fact]
    public void A_very_long_timeout_is_capped_instead_of_throwing()
    {
        var redactor = Redactor.Create(TimeSpan.FromDays(30));

        var result = redactor.Scrub(new string('x', 1_000));

        Assert.Equal(ScrubOutcome.Clean, result.Outcome);
    }

    [Fact]
    public void A_key_block_larger_than_the_end_search_window_is_redacted_whole()
    {
        var line = string.Concat(Enumerable.Repeat(Shapes.Body, 3))[..64];
        var body = string.Join('\n', Enumerable.Repeat(line, 700));
        var input = Shapes.Join("-----BEGIN PGP PRIV", "ATE KEY BLOCK-----\n\n", body, "\n-----END PGP PRIV", "ATE KEY BLOCK-----");

        var result = Redactor.Default.Scrub(input);

        Assert.DoesNotContain(line, result.Text, StringComparison.Ordinal);
        Assert.StartsWith("[REDACTED:private-key]", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_truncated_key_does_not_swallow_the_short_lines_after_it()
    {
        var input = Shapes.Join("-----BEGIN RSA PRIV", "ATE KEY-----\n", Shapes.Body, Shapes.Body, "\nexit status 1\nDone in 3 s");

        var result = Redactor.Default.Scrub(input);

        Assert.Equal("[REDACTED:private-key]\nexit status 1\nDone in 3 s", result.Text);
    }
}
