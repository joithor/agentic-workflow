using Aw.Kernel.Testing;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Text;

/// <summary>The shared sanitizer (API spec \u00A73.6): stored text shows as written, controls and invisible marks made visible, cut to one line.</summary>
[Lane(TestLane.Unit)]
public sealed class SafeTextTests
{
    [Theory]
    [InlineData("a\u001Bb", @"a\x1Bb")]
    [InlineData("a\u0007b", @"a\x07b")]
    [InlineData("a\u0000b", @"a\x00b")]
    [InlineData("a\u009Bb", @"a\x9Bb")]
    [InlineData("a\u007Fb", @"a\x7Fb")]
    public void Escape_turns_controls_into_visible_text(string text, string expected) =>
        Expect.Equal(expected, SafeText.Escape(text));

    [Fact]
    public void Escape_removes_an_osc_8_link_and_a_csi_colour()
    {
        const string text = "see \u001B]8;;https://evil.example\u0007here\u001B]8;;\u0007 and \u001B[31mred\u001B[0m";

        var escaped = SafeText.Escape(text);

        Expect.Equal(@"see \x1B]8;;https://evil.example\x07here\x1B]8;;\x07 and \x1B[31mred\x1B[0m", escaped);
        Expect.True(!escaped.Any(char.IsControl));
    }

    [Theory]
    [InlineData("a\u200Bb", @"a\u{200B}b")]
    [InlineData("a\u202Eb", @"a\u{202E}b")]
    [InlineData("a\u2066b\u2069", @"a\u{2066}b\u{2069}")]
    [InlineData("\uFEFFa", @"\u{FEFF}a")]
    [InlineData("a\u2028b", @"a\u{2028}b")]
    [InlineData("a\U000E0041b", @"a\u{E0041}b")]
    [InlineData("a\uD800b", @"a\u{D800}b")]
    public void Escape_marks_zero_width_and_bidi_characters(string text, string expected) =>
        Expect.Equal(expected, SafeText.Escape(text));

    [Fact]
    public void Escape_keeps_plain_text_and_newlines_for_Line_to_fold() =>
        Expect.Equal("Fix ENG-142 — naïve café 🚀\nnext", SafeText.Escape("Fix ENG-142 — naïve café 🚀\nnext"));

    [Theory]
    [InlineData("  one\ntwo\r\nthree\tfour  ", 80, "one two three four")]
    [InlineData("one\n\n\ntwo", 80, "one two")]
    [InlineData("abcdefghij", 10, "abcdefghij")]
    [InlineData("abcdefghijk", 10, "abcdefghi…")]
    [InlineData("abcdefgh🚀", 9, "abcdefgh…")]
    [InlineData("abcdefg🚀x", 9, "abcdefg…")]
    public void Line_folds_newlines_and_cuts_at_max(string text, int max, string expected) =>
        Expect.Equal(expected, SafeText.Line(text, max));

    [Fact]
    public void Line_refuses_a_max_below_one() =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => SafeText.Line("a", 0));

    [Fact]
    public void Clean_scrubs_then_escapes_then_folds() =>
        Expect.Equal(@"a \x1B[2J b", SafeText.Clean(new PassThroughScrubber(), "a\n\u001B[2J\nb", 80));
}
