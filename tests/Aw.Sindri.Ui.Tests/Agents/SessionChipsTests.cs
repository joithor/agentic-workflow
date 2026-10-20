using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents;

/// <summary>The session chips (collector spec CO14, CU-6): glyph and word, the word always present.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionChipsTests
{
    [Theory]
    [InlineData("running", "●", "WORKING")]
    [InlineData("asking", "◐", "ASKING YOU")]
    [InlineData("idle", "○", "IDLE")]
    [InlineData("unknown", "○", "UNKNOWN")]
    public void Each_state_maps_to_its_glyph_and_word(string state, string glyph, string word)
    {
        var chip = SessionChips.For(state);

        Assert.Equal((glyph, word, glyph + " " + word), (chip.Glyph, chip.Word, chip.Text));
    }

    [Fact]
    public void An_unknown_state_renders_as_its_word()
    {
        var chip = SessionChips.For("dozing");

        Assert.Equal(("○", "DOZING"), (chip.Glyph, chip.Word));
        Assert.Equal("dozing", SessionChips.Word("dozing"));
    }
}
