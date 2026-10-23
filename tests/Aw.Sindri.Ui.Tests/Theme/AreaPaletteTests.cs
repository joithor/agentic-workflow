using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>The whole map's area palette (whole-map spec §3.2): bounded, distinct, and readable as a mark on the canvas and on a card.</summary>
[Lane(TestLane.Unit)]
public sealed class AreaPaletteTests
{
    private static readonly TokenFile _tokens = TokenFile.Load();

    private static readonly string[] _names = ["Area1", "Area2", "Area3", "Area4", "Area5", "Area6"];

    [Fact]
    public void The_palette_is_as_wide_as_the_view_models_says() =>
        Assert.Equal(ViewModels.Context.Whole.WholePalette.Colours, _names.Length);

    [Fact]
    public void Each_area_colour_is_a_non_text_mark_on_the_canvas_and_on_a_card()
    {
        Assert.All(_names, name =>
        {
            Assert.True(_tokens.Contrast(name, "Canvas") >= 3.0, name + " on the canvas");
            Assert.True(_tokens.Contrast(name, "Surface1") >= 3.0, name + " on a card");
        });
    }

    [Fact]
    public void No_two_area_colours_are_the_same() =>
        Assert.Equal(_names.Length, _names.Select(_tokens.Color).Distinct(StringComparer.OrdinalIgnoreCase).Count());
}
