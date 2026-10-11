namespace Aw.Evidence.Core.Images;

/// <summary>Perceptual colour distance in the YIQ space, with translucent pixels blended over white.</summary>
internal static class YiqColor
{
    /// <summary>The largest possible YIQ delta (black against white); a threshold scales it.</summary>
    internal const double MaxDelta = 35215;

    /// <summary>
    /// The squared YIQ distance between pixel <paramref name="i"/> of <paramref name="a"/> and pixel <paramref name="j"/>
    /// of <paramref name="b"/>. Negative when the first pixel is brighter. With <paramref name="brightnessOnly"/>, the
    /// signed difference in luma only.
    /// </summary>
    internal static double Delta(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, int i, int j, bool brightnessOnly)
    {
        if (a[i] == b[j] && a[i + 1] == b[j + 1] && a[i + 2] == b[j + 2] && a[i + 3] == b[j + 3])
        {
            return 0;
        }

        var (r1, g1, b1) = Blended(a, i);
        var (r2, g2, b2) = Blended(b, j);
        var y1 = Y(r1, g1, b1);
        var y2 = Y(r2, g2, b2);
        var y = y1 - y2;
        if (brightnessOnly)
        {
            return y;
        }

        var iq = I(r1, g1, b1) - I(r2, g2, b2);
        var q = Q(r1, g1, b1) - Q(r2, g2, b2);
        var delta = (0.5053 * y * y) + (0.299 * iq * iq) + (0.1957 * q * q);
        return y1 > y2 ? -delta : delta;
    }

    /// <summary>The pixel's luma blended toward white by <paramref name="alpha"/>, for the faded diff background.</summary>
    internal static byte FadedGrey(ReadOnlySpan<byte> image, int i, double alpha)
    {
        var y = Y(image[i], image[i + 1], image[i + 2]);
        return (byte)Math.Round(Blend(y, alpha * image[i + 3] / 255), MidpointRounding.AwayFromZero);
    }

    private static (double R, double G, double B) Blended(ReadOnlySpan<byte> image, int i)
    {
        double r = image[i];
        double g = image[i + 1];
        double b = image[i + 2];
        var a = image[i + 3];
        return a < 255 ? (Blend(r, a / 255.0), Blend(g, a / 255.0), Blend(b, a / 255.0)) : (r, g, b);
    }

    private static double Blend(double c, double a) => 255 + ((c - 255) * a);

    private static double Y(double r, double g, double b) => (r * 0.29889531) + (g * 0.58662247) + (b * 0.11448223);

    private static double I(double r, double g, double b) => (r * 0.59597799) - (g * 0.27417610) - (b * 0.32180189);

    private static double Q(double r, double g, double b) => (r * 0.21147017) - (g * 0.52261711) + (b * 0.31114694);
}
