namespace Aw.Redaction.Tests;

[Trait("Lane", "unit")]
public sealed class AdversarialInputTests
{
    [Fact]
    public void A_text_full_of_placeholders_and_secrets_is_scrubbed_not_blocked()
    {
        var key = Shapes.Join("gh", "p_", Shapes.Body, "Ab3dE6gH9");
        var input = string.Concat(Enumerable.Repeat("[REDACTED:github-token] " + key + " ", 60_000));

        var result = Redactor.Default.Scrub(input);

        Assert.Equal(ScrubOutcome.Redacted, result.Outcome);
        Assert.Equal(60_000, result.Hits.Count);
    }

    [Fact]
    public void A_megabyte_of_unterminated_begin_lines_is_scrubbed_not_blocked()
    {
        var input = string.Concat(Enumerable.Repeat("-----BEGIN RSA PRIV" + "ATE KEY-----\n", 1_000_000 / 32));

        var result = Redactor.Default.Scrub(input);

        Assert.NotEqual(ScrubOutcome.Blocked, result.Outcome);
    }

    [Fact]
    public void The_timeout_grows_with_the_input_so_size_alone_never_blocks()
    {
        // 100 ms per pattern is far too little for 4 MB in one window; the budget is per window of input.
        var redactor = new Redactor(BuiltinRules.All, TimeSpan.FromMilliseconds(100));
        var chunk = Shapes.Join("Lorem ipsum dolor sit amet, see https://example.com/a?b=c gh", "p_", Shapes.Body, "Ab3dE6gH9 \n");
        var input = string.Concat(Enumerable.Repeat(chunk, 4_000_000 / chunk.Length));

        var result = redactor.Scrub(input);

        Assert.Equal(ScrubOutcome.Redacted, result.Outcome);
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
