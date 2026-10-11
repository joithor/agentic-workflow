using Aw.Evidence.Core;
using Aw.Evidence.Core.Images;
using Aw.Evidence.Infrastructure.Images;

namespace Aw.Evidence.Tests.Infrastructure;

public sealed class PngCodecTests
{
    private readonly PngCodec _codec = new();

    [Fact, Trait("Lane", "unit")]
    public void The_crc_matches_the_png_specification() =>
        Assert.Equal(0xAE426082u, Crc32.Compute("IEND"u8));

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Truecolour_with_alpha_decodes_through_every_filter(int filter)
    {
        var rows = Gradient(7, 5, channels: 4);
        var png = new PngFixture { Width = 7, Height = 5, ColorType = 6, Rows = rows, Filter = filter }.Build();

        var image = _codec.Decode(png).Value;

        Assert.Equal(rows.SelectMany(r => r), image.Pixels.ToArray());
    }

    [Fact, Trait("Lane", "unit")]
    public void Truecolour_decodes_opaque_and_its_trns_colour_transparent()
    {
        byte[][] rows = [[10, 20, 30, 1, 2, 3]];
        var png = new PngFixture { Width = 2, Height = 1, ColorType = 2, Rows = rows, Filter = -1 }.With("tRNS", [0, 1, 0, 2, 0, 3]).Build();

        var image = _codec.Decode(png).Value;

        Assert.Equal(0x0A141EFFu, image.PixelAt(0, 0));
        Assert.Equal(0x01020300u, image.PixelAt(1, 0));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_palette_image_decodes_through_its_palette_and_alpha_table()
    {
        byte[][] rows = [[0, 1, 2], [2, 1, 0]];
        var png = new PngFixture { Width = 3, Height = 2, ColorType = 3, Rows = rows }
            .With("PLTE", [255, 0, 0, 0, 255, 0, 0, 0, 255])
            .With("tRNS", [128])
            .Build();

        var image = _codec.Decode(png).Value;

        Assert.Equal([0xFF000080u, 0x00FF00FFu, 0x0000FFFFu], Row(image, 0));
        Assert.Equal([0x0000FFFFu, 0x00FF00FFu, 0xFF000080u], Row(image, 1));
    }

    [Fact, Trait("Lane", "unit")]
    public void Grayscale_and_grayscale_with_alpha_decode_to_grey_rgba()
    {
        var grey = new PngFixture { Width = 2, Height = 1, ColorType = 0, Rows = [[0x40, 0xC0]] }.With("tRNS", [0, 0xC0]).Build();
        var greyAlpha = new PngFixture { Width = 1, Height = 2, ColorType = 4, Rows = [[0x80, 0x10], [0xFF, 0xFF]], Filter = 2 }.Build();

        Assert.Equal([0x404040FFu, 0xC0C0C000u], Row(_codec.Decode(grey).Value, 0));
        var both = _codec.Decode(greyAlpha).Value;
        Assert.Equal(0x80808010u, both.PixelAt(0, 0));
        Assert.Equal(0xFFFFFFFFu, both.PixelAt(0, 1));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_chromium_screenshot_decodes()
    {
        // Captured with Playwright's Chromium (24x18 viewport): a red 20x12 block, a half-transparent blue 12x6 block below
        // it, white elsewhere. Chromium writes 8-bit RGB (colour type 2) over two IDAT chunks.
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABgAAAASCAIAAADOjonJAAAAMklEQVR4nOyTMQ4AAAQDEf/m54xGAzFIb25vaFINGhDVFloCIoh+itisf5t7G8HYl6IEAAD//52o/BgAAAAGSURBVAMAPF4GIAeJqIMAAAAASUVORK5CYII=");

        var image = _codec.Decode(png).Value;

        Assert.Equal((24, 18), (image.Width, image.Height));
        Assert.Equal(0xFF0000FFu, image.PixelAt(0, 0));
        Assert.Equal(0xFF0000FFu, image.PixelAt(19, 11));
        Assert.InRange(image.PixelAt(0, 12) >> 24, 0x7Fu, 0x80u);
        Assert.Equal(0xFFu, (image.PixelAt(0, 12) >> 8) & 0xFF);
        Assert.Equal(0xFFFFFFFFu, image.PixelAt(23, 17));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData((byte)16, (byte)0, "bit depth 16")]
    [InlineData((byte)8, (byte)1, "interlaced")]
    public void Unsupported_variants_are_named_in_the_error(byte bitDepth, byte interlace, string named)
    {
        var png = new PngFixture { Width = 1, Height = 1, ColorType = 6, BitDepth = bitDepth, Interlace = interlace, Rows = [[1, 2, 3, 4]] }.Build();

        var decoded = _codec.Decode(png);

        Assert.Equal(EvidenceErrors.ImageInvalid.Code, decoded.Error.Code);
        Assert.Contains(named, decoded.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_corrupt_chunk_and_a_palette_image_without_a_palette_are_errors()
    {
        var png = new PngFixture { Width = 1, Height = 1, ColorType = 6, Rows = [[1, 2, 3, 4]] }.Build();
        png[^20] ^= 0xFF;   // inside the last IDAT's data: its CRC no longer matches
        var paletteless = new PngFixture { Width = 1, Height = 1, ColorType = 3, Rows = [[0]] }.Build();

        Assert.Contains("CRC", _codec.Decode(png).Error.Message, StringComparison.Ordinal);
        Assert.Contains("PLTE", _codec.Decode(paletteless).Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_truncated_file_is_an_error_not_an_exception()
    {
        var png = new PngFixture { Width = 4, Height = 4, ColorType = 6, Rows = Gradient(4, 4, 4) }.Build();

        Assert.Equal(EvidenceErrors.ImageInvalid.Code, _codec.Decode(png.AsSpan(0, png.Length - 30)).Error.Code);
        Assert.Equal(EvidenceErrors.ImageInvalid.Code, _codec.Decode(png.AsSpan(0, 20)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_encoded_image_is_a_valid_rgba_png()
    {
        var png = _codec.Encode(RgbaImage.Filled(3, 2, 0x11223344));

        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
        Assert.Equal("IHDR"u8.ToArray(), png[12..16]);
        Assert.Equal([0, 0, 0, 3, 0, 0, 0, 2, 8, 6, 0, 0, 0], png[16..29]);
        Assert.Equal("IEND"u8.ToArray(), png[^8..^4]);
    }

    private static uint[] Row(RgbaImage image, int y) => [.. Enumerable.Range(0, image.Width).Select(x => image.PixelAt(x, y))];

    private static byte[][] Gradient(int width, int height, int channels) =>
        [.. Enumerable.Range(0, height).Select(y => Enumerable.Range(0, width * channels).Select(i => (byte)((i * 37) + (y * 91) + (i % channels * 13))).ToArray())];
}
