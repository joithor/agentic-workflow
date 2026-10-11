using System.Buffers.Binary;
using System.Globalization;

namespace Aw.Evidence.Infrastructure.Images;

/// <summary>An IHDR chunk this decoder accepts: 8-bit, colour type 0, 2, 3, 4 or 6, not interlaced.</summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="ColorType">0 grey, 2 RGB, 3 palette, 4 grey + alpha, 6 RGBA.</param>
internal sealed record PngHeader(int Width, int Height, byte ColorType)
{
    // A decoded image is width * height * 4 bytes in one array; keep it well inside what an array can hold.
    private const long MaxPixels = 1L << 27;

    /// <summary>Bytes per pixel in a scanline.</summary>
    internal int Channels => ColorType switch { 0 or 3 => 1, 4 => 2, 2 => 3, _ => 4 };

    /// <summary>Bytes per scanline, without its filter byte.</summary>
    internal int RowLength => Width * Channels;

    /// <summary>Reads and checks an IHDR chunk's data.</summary>
    /// <param name="data">The 13 bytes.</param>
    /// <returns>The header.</returns>
    /// <exception cref="InvalidPngException">The header is malformed or names something this decoder doesn't read.</exception>
    internal static PngHeader Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length != 13)
        {
            throw new InvalidPngException("IHDR is not 13 bytes");
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(data);
        var height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
        if (width <= 0 || height <= 0 || (long)width * height > MaxPixels)
        {
            throw new InvalidPngException(string.Create(CultureInfo.InvariantCulture, $"a {width}x{height} image is empty or too large"));
        }

        Supported(bitDepth: data[8], colorType: data[9], compression: data[10], filter: data[11], interlace: data[12]);
        return new(width, height, data[9]);
    }

    private static void Supported(byte bitDepth, byte colorType, byte compression, byte filter, byte interlace)
    {
        var problem = bitDepth != 8 ? $"bit depth {bitDepth} (only 8-bit PNGs are read)"
            : colorType is not (0 or 2 or 3 or 4 or 6) ? $"colour type {colorType}"
            : compression != 0 || filter != 0 ? "an unknown compression or filter method"
            : interlace != 0 ? "an interlaced (Adam7) image" : null;
        if (problem is not null)
        {
            throw new InvalidPngException($"unsupported PNG: {problem}");
        }
    }
}
