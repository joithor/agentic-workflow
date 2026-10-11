using System.Globalization;
using Aw.Evidence.Core.Geometry;
using Aw.Kernel;

namespace Aw.Evidence.Core.Images;

/// <summary>An 8-bit RGBA image in memory: four bytes per pixel, rows top to bottom, no padding.</summary>
public sealed class RgbaImage
{
    private readonly byte[] _pixels;

    private RgbaImage(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        _pixels = pixels;
    }

    /// <summary>Width in pixels.</summary>
    public int Width { get; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; }

    /// <summary>The RGBA bytes.</summary>
    public ReadOnlySpan<byte> Pixels => _pixels;

    /// <summary>Wraps a copy of <paramref name="pixels"/> as a <paramref name="width"/> x <paramref name="height"/> image.</summary>
    /// <param name="width">Width, at least 1.</param>
    /// <param name="height">Height, at least 1.</param>
    /// <param name="pixels">Exactly width * height * 4 bytes.</param>
    /// <returns>The image, or AW-EVIDENCE-012 when the sizes don't match.</returns>
    public static Result<RgbaImage> Create(int width, int height, ReadOnlySpan<byte> pixels)
    {
        if (width <= 0 || height <= 0 || (long)width * height * 4 != pixels.Length)
        {
            return EvidenceErrors.ImageInvalid.With(
                string.Create(CultureInfo.InvariantCulture, $"{pixels.Length} bytes are not a {width}x{height} RGBA image ({(long)Math.Max(width, 0) * Math.Max(height, 0) * 4} bytes)."));
        }

        return new RgbaImage(width, height, pixels.ToArray());
    }

    /// <summary>A <paramref name="width"/> x <paramref name="height"/> image with every pixel set to one colour.</summary>
    /// <param name="width">Width, at least 1.</param>
    /// <param name="height">Height, at least 1.</param>
    /// <param name="rgba">The colour, as 0xRRGGBBAA.</param>
    /// <returns>The image.</returns>
    public static RgbaImage Filled(int width, int height, uint rgba)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = (byte)(rgba >> 24);
            pixels[i + 1] = (byte)(rgba >> 16);
            pixels[i + 2] = (byte)(rgba >> 8);
            pixels[i + 3] = (byte)rgba;
        }

        return new RgbaImage(width, height, pixels);
    }

    /// <summary>The bounds of the whole image.</summary>
    /// <returns>0,0 width x height.</returns>
    public Region Bounds() => new(0, 0, Width, Height);

    /// <summary>The pixel at <paramref name="x"/>, <paramref name="y"/> as 0xRRGGBBAA.</summary>
    /// <param name="x">Column.</param>
    /// <param name="y">Row.</param>
    /// <returns>The colour.</returns>
    public uint PixelAt(int x, int y)
    {
        var i = ((y * Width) + x) * 4;
        return ((uint)_pixels[i] << 24) | ((uint)_pixels[i + 1] << 16) | ((uint)_pixels[i + 2] << 8) | _pixels[i + 3];
    }

    internal static RgbaImage Own(int width, int height, byte[] pixels) => new(width, height, pixels);
}
