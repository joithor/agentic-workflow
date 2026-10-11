using Aw.Evidence.Core;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Images;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Images;

public sealed class ImageOpsTests
{
    private static readonly RgbaImage _numbered = TestImages.Draw(8, 6, (x, y) => (uint)((x << 24) | (y << 16) | 0xFF));

    [Fact, Trait("Lane", "unit")]
    public void A_crop_copies_exactly_the_region()
    {
        var crop = ImageOps.Crop(_numbered, new Region(2, 1, 3, 4)).Value;

        Assert.Equal((3, 4), (crop.Width, crop.Height));
        Assert.Equal(_numbered.PixelAt(2, 1), crop.PixelAt(0, 0));
        Assert.Equal(_numbered.PixelAt(4, 4), crop.PixelAt(2, 3));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(6, 0, 3, 2)]
    [InlineData(-1, 0, 2, 2)]
    [InlineData(0, 0, 0, 2)]
    [InlineData(0, 5, 2, 2)]
    public void A_region_not_wholly_inside_is_an_error_never_a_silent_clip(int x, int y, int w, int h)
    {
        var result = ImageOps.Crop(_numbered, new Region(x, y, w, h));

        Assert.Equal(EvidenceErrors.RegionOutside.Code, result.Error.Code);
        Assert.Contains("outside the 8x6 image", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_padded_crop_grows_the_box_and_clamps_to_the_image()
    {
        var crop = ImageOps.CropPadded(_numbered, new Region(1, 1, 2, 2), pad: 3).Value;

        Assert.Equal((8, 6), (crop.Width, crop.Height));
        Assert.Equal(_numbered.PixelAt(0, 0), crop.PixelAt(0, 0));
    }

    [Fact, Trait("Lane", "unit")]
    public void Side_by_side_lays_out_three_panels_on_white_with_12_px_gaps()
    {
        var design = RgbaImage.Filled(2, 3, 0x111111FF);
        var implementation = RgbaImage.Filled(4, 2, 0x222222FF);
        var diff = RgbaImage.Filled(1, 1, 0x333333FF);

        var sheet = ImageOps.SideBySide(design, implementation, diff);

        Assert.Equal((2 + 4 + 1 + 24, 3), (sheet.Width, sheet.Height));
        Assert.Equal(0x111111FFu, sheet.PixelAt(1, 2));
        Assert.Equal(0xFFFFFFFFu, sheet.PixelAt(2, 0));
        Assert.Equal(0x222222FFu, sheet.PixelAt(14, 0));
        Assert.Equal(0xFFFFFFFFu, sheet.PixelAt(14, 2));
        Assert.Equal(0x333333FFu, sheet.PixelAt(30, 0));
    }

    [Fact, Trait("Lane", "unit")]
    public void Raw_pixels_must_match_the_declared_size() =>
        Assert.Equal(EvidenceErrors.ImageInvalid.Code, RgbaImage.Create(2, 2, new byte[15]).Error.Code);
}
