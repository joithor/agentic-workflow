using Aw.Evidence.Core;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Images;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Images;

public sealed class PixelMatchTests
{
    private const uint White = 0xFFFFFFFF;
    private const uint Black = 0x000000FF;
    private const uint Red = 0xFF0000FF;
    private const uint Yellow = 0xFFFF00FF;

    // Black left, white right, a grey anti-aliased column between them.
    private static RgbaImage Edge(byte grey) => TestImages.Draw(5, 5, (x, _) => x switch
    {
        < 2 => Black,
        2 => ((uint)grey << 24) | ((uint)grey << 16) | ((uint)grey << 8) | 0xFF,
        _ => White,
    });

    [Fact, Trait("Lane", "unit")]
    public void Identical_images_have_no_differing_pixels()
    {
        var image = TestImages.Checkerboard(4, 4, Black, White);

        var result = PixelMatch.Compare(image, image, PixelMatchOptions.Default).Value;

        Assert.Equal(0, result.DiffPixels);
        Assert.Equal(16, result.Total);
        Assert.Null(result.Changed);
        Assert.Equal(0, result.DiffPercent);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_changed_pixel_is_counted_painted_red_and_boxed()
    {
        var before = RgbaImage.Filled(6, 4, White);
        var after = TestImages.Draw(6, 4, (x, y) => (x, y) is (4, 1) or (2, 3) ? Black : White);

        var result = PixelMatch.Compare(before, after, PixelMatchOptions.Default).Value;

        Assert.Equal(2, result.DiffPixels);
        Assert.Equal(new Region(2, 1, 3, 3), result.Changed);
        Assert.Equal(Red, result.Diff.PixelAt(4, 1));
        Assert.Equal(8.33, result.DiffPercent);
    }

    [Fact, Trait("Lane", "unit")]
    public void Unchanged_pixels_are_drawn_as_faded_grey()
    {
        var image = RgbaImage.Filled(2, 2, Black);

        var diff = PixelMatch.Compare(image, image, PixelMatchOptions.Default).Value.Diff;

        Assert.Equal(0xE6E6E6FFu, diff.PixelAt(0, 0));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_colour_shift_under_the_threshold_is_not_a_difference()
    {
        var before = RgbaImage.Filled(3, 3, 0x808080FF);
        var after = RgbaImage.Filled(3, 3, 0x838383FF);

        Assert.Equal(0, PixelMatch.Compare(before, after, PixelMatchOptions.Default).Value.DiffPixels);
        Assert.Equal(9, PixelMatch.Compare(before, after, new PixelMatchOptions(0, IncludeAA: false)).Value.DiffPixels);
    }

    [Fact, Trait("Lane", "unit")]
    public void Anti_aliased_edge_pixels_are_ignored_unless_included()
    {
        var ignored = PixelMatch.Compare(Edge(128), Edge(100), PixelMatchOptions.Default).Value;
        var counted = PixelMatch.Compare(Edge(128), Edge(100), new PixelMatchOptions(0.1, IncludeAA: true)).Value;

        Assert.Equal(0, ignored.DiffPixels);
        Assert.Equal(Yellow, ignored.Diff.PixelAt(2, 2));
        Assert.Equal(5, counted.DiffPixels);
    }

    [Fact, Trait("Lane", "unit")]
    public void Translucent_pixels_are_compared_as_blended_over_white() =>
        Assert.Equal(0, PixelMatch.Compare(RgbaImage.Filled(2, 2, 0x00000000), RgbaImage.Filled(2, 2, White), new PixelMatchOptions(0, IncludeAA: false)).Value.DiffPixels);

    [Fact, Trait("Lane", "unit")]
    public void Images_of_different_sizes_are_a_size_mismatch()
    {
        var result = PixelMatch.Compare(RgbaImage.Filled(2, 2, White), RgbaImage.Filled(3, 2, White), PixelMatchOptions.Default);

        Assert.Equal(EvidenceErrors.SizeMismatch.Code, result.Error.Code);
        Assert.Contains("2x2 vs 3x2", result.Error.Message, StringComparison.Ordinal);
    }
}
