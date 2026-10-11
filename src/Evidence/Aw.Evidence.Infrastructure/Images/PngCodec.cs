using Aw.Evidence.Core;
using Aw.Evidence.Core.Images;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Images;

/// <summary>
/// PNG in and out without an imaging library: decodes 8-bit, non-interlaced colour types 0, 2, 3, 4 and 6 (palette and
/// tRNS included), which covers browser screenshots and design exports; encodes RGBA. Anything else is AW-EVIDENCE-012
/// naming what isn't supported.
/// </summary>
internal sealed class PngCodec : IImageCodec
{
    /// <inheritdoc/>
    public Result<RgbaImage> Decode(ReadOnlySpan<byte> encoded)
    {
        try
        {
            var parts = PngChunkReader.Read(encoded);
            var raw = PngScanlines.Decode(parts.Header, parts.Compressed);
            return RgbaImage.Create(parts.Header.Width, parts.Header.Height, PngPixels.ToRgba(parts, raw));
        }
        catch (InvalidPngException ex)
        {
            return EvidenceErrors.ImageInvalid.With(ex.Message);
        }
    }

    /// <inheritdoc/>
    public byte[] Encode(RgbaImage image) => PngWriter.Write(image);
}
