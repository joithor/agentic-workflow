using Aw.CodeIndex.Infrastructure.Scrubbing;

namespace Aw.CodeIndex.Tests.Scrubbing;

public sealed class RedactingBodyScrubberTests
{
    // Built from two halves so this file never holds a complete key shape.
    private const string Key = "AK" + "IA" + "0123456789ABCDEF";

    [Fact, Trait("Lane", "unit")]
    public void A_secret_is_redacted_before_the_cut_so_a_cut_cannot_leave_half_of_it()
    {
        var text = "var id = \"" + Key + "\";";

        var scrubbed = new RedactingBodyScrubber().Scrub(text, maxCharacters: 24);

        Assert.DoesNotContain("AKIA", scrubbed, StringComparison.Ordinal);
        Assert.True(scrubbed.Length <= 24);
    }

    [Fact, Trait("Lane", "unit")]
    public void Plain_text_passes_whole_when_it_fits()
        => Assert.Equal("void Run()", new RedactingBodyScrubber().Scrub("void Run()", 400));
}
