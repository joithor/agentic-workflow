using System.Diagnostics;

namespace Aw.Redaction.Tests;

[Trait("Lane", "unit")]
public sealed class AdversarialInputTests
{
    [Fact]
    public void A_text_full_of_placeholders_and_secrets_scrubs_in_linear_time()
    {
        var key = Shapes.Join("gh", "p_", Shapes.Body, "Ab3dE6gH9");
        var input = string.Concat(Enumerable.Repeat("[REDACTED:github-token] " + key + " ", 60_000));
        var clock = Stopwatch.StartNew();

        var result = Redactor.Default.Scrub(input);

        clock.Stop();
        Assert.Equal(60_000, result.Hits.Count);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(6), $"took {clock.Elapsed}; the placeholder check must not be quadratic");
    }

    [Fact]
    public void A_megabyte_of_unterminated_begin_lines_is_scrubbed_not_blocked()
    {
        var input = string.Concat(Enumerable.Repeat("-----BEGIN RSA PRIV" + "ATE KEY-----\n", 1_000_000 / 32));

        var result = Redactor.Default.Scrub(input);

        Assert.NotEqual(ScrubOutcome.Blocked, result.Outcome);
    }
}
