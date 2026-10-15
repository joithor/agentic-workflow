namespace Aw.Redaction.Tests;

/// <summary>Real-clock scale cases: megabytes of input against real regex timeouts. They are slow and sensitive to
/// machine load, so they are in the heavy lane, outside the unit lane and its budget (CI's unfiltered run still executes them); the unit lane checks the same rules on the timeout arithmetic
/// (<see cref="AdversarialInputTests"/>) and on inputs of a few hundred kilobytes.</summary>
[Trait("Lane", "heavy")]
public sealed class HeavyScaleTests
{
    [Fact]
    public void The_timeout_grows_with_the_input_so_size_alone_never_blocks()
    {
        // 4 MB takes seconds, over twice 500 ms but inside its 32 windows of it (16 s): the budget is per window of
        // input. The margin is wide on purpose, so a loaded machine doesn't block a legitimate scrub.
        var redactor = new Redactor(BuiltinRules.All, TimeSpan.FromMilliseconds(500));
        var chunk = Shapes.Join("Lorem ipsum dolor sit amet, see https://example.com/a?b=c gh", "p_", Shapes.Body, "Ab3dE6gH9 \n");
        var input = string.Concat(Enumerable.Repeat(chunk, 4_000_000 / chunk.Length));

        var result = redactor.Scrub(input);

        Assert.Equal(ScrubOutcome.Redacted, result.Outcome);
    }

    [Fact]
    public void Sixty_thousand_placeholders_and_secrets_are_scrubbed_not_blocked()
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
    public void Two_megabytes_of_mixed_text_scrub_within_the_default_timeout()
    {
        var key = Shapes.Join(" gh", "p_", Shapes.Body, "Ab3dE6gH9 ");
        var chunk = "Lorem ipsum dolor sit amet, the key is unset; see https://example.com/a?b=c " + key;
        var input = string.Concat(Enumerable.Repeat(chunk, 2_000_000 / chunk.Length));

        var result = Redactor.Default.Scrub(input);

        Assert.Equal(ScrubOutcome.Redacted, result.Outcome);
        Assert.DoesNotContain(Shapes.Tail, result.Text, StringComparison.Ordinal);
    }
}
