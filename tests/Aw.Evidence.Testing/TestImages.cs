using Aw.Evidence.Core.Images;

namespace Aw.Evidence.Testing;

/// <summary>Small deterministic test images.</summary>
public static class TestImages
{
    /// <summary>A checkerboard of two colours (0xRRGGBBAA), starting with <paramref name="a"/> at 0,0.</summary>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    /// <param name="a">The first colour.</param>
    /// <param name="b">The second colour.</param>
    /// <returns>The image.</returns>
    public static RgbaImage Checkerboard(int width, int height, uint a, uint b) =>
        Draw(width, height, (x, y) => (x + y) % 2 == 0 ? a : b);

    /// <summary>An image whose pixel colours come from <paramref name="colour"/>.</summary>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    /// <param name="colour">The colour (0xRRGGBBAA) at x, y.</param>
    /// <returns>The image.</returns>
    public static RgbaImage Draw(int width, int height, Func<int, int, uint> colour)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var c = colour(x, y);
                var i = ((y * width) + x) * 4;
                (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = ((byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)c);
            }
        }

        return RgbaImage.Create(width, height, pixels).Value;
    }
}
