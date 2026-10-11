using Aw.Evidence.Core;
using Aw.Evidence.Core.Images;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>The IImageCodec contract: encode then decode returns the same pixels; bytes it can't read are AW-EVIDENCE-012.</summary>
public abstract class ImageCodecContract
{
    /// <summary>Creates the codec under test.</summary>
    /// <returns>The codec.</returns>
    protected abstract IImageCodec Create();

    /// <summary>Every pixel (translucent ones included) survives a round trip.</summary>
    [Fact]
    public void An_image_round_trips_pixel_for_pixel()
    {
        var codec = Create();
        var image = TestImages.Checkerboard(5, 3, 0xFF000080, 0x00FF00FF);

        var decoded = codec.Decode(codec.Encode(image)).Value;

        Expect.That(decoded.Width == image.Width && decoded.Height == image.Height, "the size survives a round trip");
        Expect.That(image.Pixels.SequenceEqual(decoded.Pixels), "every pixel survives a round trip");
    }

    /// <summary>A one-pixel image round trips.</summary>
    [Fact]
    public void A_single_pixel_round_trips()
    {
        var codec = Create();
        var image = RgbaImage.Filled(1, 1, 0x12345678);

        Expect.That(codec.Decode(codec.Encode(image)).Value.PixelAt(0, 0) == 0x12345678u, "a one-pixel image round trips");
    }

    /// <summary>Bytes that are not an image are an error, not an exception.</summary>
    [Fact]
    public void Garbage_is_an_image_error()
    {
        var codec = Create();

        Expect.That(codec.Decode("definitely not an image"u8).Error.Code == EvidenceErrors.ImageInvalid.Code, "garbage is AW-EVIDENCE-012");
        Expect.That(codec.Decode([]).Error.Code == EvidenceErrors.ImageInvalid.Code, "no bytes is AW-EVIDENCE-012");
    }
}
