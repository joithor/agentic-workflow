namespace Aw.Evidence.Core.Images;

/// <summary>
/// Detects anti-aliased pixels: a pixel whose 3x3 neighbourhood holds both a darker and a brighter neighbour, at most
/// two identical ones, and where the darkest or brightest neighbour sits in a flat area in both images.
/// </summary>
internal static class AntiAliasing
{
    internal static bool IsAntiAliased(ImagePair pair, int x, int y)
    {
        var extremes = Extremes(pair, x, y);
        return extremes is { } e
            && ((HasManySiblings(pair.Image, pair, e.DarkX, e.DarkY) && HasManySiblings(pair.Other, pair, e.DarkX, e.DarkY))
                || (HasManySiblings(pair.Image, pair, e.BrightX, e.BrightY) && HasManySiblings(pair.Other, pair, e.BrightX, e.BrightY)));
    }

    // The darkest and brightest neighbours by luma; null when more than two neighbours equal the pixel or one side is missing.
    private static (int DarkX, int DarkY, int BrightX, int BrightY)? Extremes(ImagePair pair, int x, int y)
    {
        var pos = pair.Offset(x, y);
        var zeroes = pair.OnEdge(x, y) ? 1 : 0;
        double min = 0, max = 0;
        (int X, int Y) dark = default, bright = default;
        foreach (var (nx, ny) in Neighbours(x, y, pair.Width, pair.Height))
        {
            var delta = YiqColor.Delta(pair.Image, pair.Image, pos, pair.Offset(nx, ny), brightnessOnly: true);
            if (delta == 0 && ++zeroes > 2)
            {
                return null;
            }

            if (delta < min)
            {
                (min, dark) = (delta, (nx, ny));
            }
            else if (delta > max)
            {
                (max, bright) = (delta, (nx, ny));
            }
        }

        return min == 0 || max == 0 ? null : (dark.X, dark.Y, bright.X, bright.Y);
    }

    private static bool HasManySiblings(ReadOnlySpan<byte> image, ImagePair pair, int x, int y)
    {
        var pos = pair.Offset(x, y);
        var zeroes = pair.OnEdge(x, y) ? 1 : 0;
        foreach (var (nx, ny) in Neighbours(x, y, pair.Width, pair.Height))
        {
            if (image.Slice(pos, 4).SequenceEqual(image.Slice(pair.Offset(nx, ny), 4)) && ++zeroes > 2)
            {
                return true;
            }
        }

        return false;
    }

    private static List<(int X, int Y)> Neighbours(int x, int y, int width, int height)
    {
        var around = new List<(int X, int Y)>(8);
        for (var nx = Math.Max(x - 1, 0); nx <= Math.Min(x + 1, width - 1); nx++)
        {
            for (var ny = Math.Max(y - 1, 0); ny <= Math.Min(y + 1, height - 1); ny++)
            {
                if (nx != x || ny != y)
                {
                    around.Add((nx, ny));
                }
            }
        }

        return around;
    }
}
