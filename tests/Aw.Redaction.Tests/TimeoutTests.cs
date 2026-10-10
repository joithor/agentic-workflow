using System.Text.RegularExpressions;

namespace Aw.Redaction.Tests;

[Trait("Lane", "unit")]
public sealed class TimeoutTests
{
    [Fact]
    public void A_pattern_that_times_out_blocks_and_withholds_the_whole_text()
    {
        var slow = new RedactionRule("slow", "^(?:a|aa)+$", RegexOptions.None);
        var redactor = new Redactor([slow], TimeSpan.FromMilliseconds(1));
        var input = new string('a', 40) + "!";

        var result = redactor.Scrub(input);

        Assert.Equal(ScrubOutcome.Blocked, result.Outcome);
        Assert.True(result.IsBlocked);
        Assert.Equal(ScrubResult.BlockedText, result.Text);
        Assert.Equal(RedactionCodes.MatchTimeout, result.BlockCode);
        Assert.Empty(result.Hits);
    }

    [Fact]
    public void A_redactor_with_its_own_timeout_still_scrubs()
    {
        var redactor = Redactor.Create(TimeSpan.FromMilliseconds(500));
        var input = Shapes.Join("lin", "_api_", Shapes.Body, Shapes.Body);

        Assert.Equal("[REDACTED:linear-key]", redactor.Scrub(input).Text);
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
