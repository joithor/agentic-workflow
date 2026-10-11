using Aw.Kernel;

namespace Aw.Evidence.Core.Images;

/// <summary>Turns image file bytes (PNG) into RGBA images and back.</summary>
public interface IImageCodec
{
    /// <summary>Decodes image bytes.</summary>
    /// <param name="encoded">The file's bytes.</param>
    /// <returns>The image, or AW-EVIDENCE-012 naming what the codec can't read.</returns>
    public Result<RgbaImage> Decode(ReadOnlySpan<byte> encoded);

    /// <summary>Encodes an image.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The file's bytes.</returns>
    public byte[] Encode(RgbaImage image);
}
