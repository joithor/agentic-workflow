namespace Aw.Evidence.Infrastructure.Images;

/// <summary>Turns raw 8-bit samples of any supported colour type into RGBA, applying PLTE and tRNS.</summary>
internal static class PngPixels
{
    /// <summary>The image as RGBA bytes.</summary>
    /// <param name="parts">The palette and transparency chunks.</param>
    /// <param name="raw">The unfiltered samples.</param>
    /// <returns>Width * height * 4 bytes.</returns>
    /// <exception cref="InvalidPngException">A palette image has no palette, or an index is outside it.</exception>
    internal static byte[] ToRgba(PngParts parts, byte[] raw)
    {
        var header = parts.Header;
        var rgba = new byte[header.Width * header.Height * 4];
        var channels = header.Channels;
        for (int p = 0, o = 0; o < rgba.Length; p += channels, o += 4)
        {
            Pixel(parts, raw.AsSpan(p, channels), rgba.AsSpan(o, 4));
        }

        return rgba;
    }

    private static void Pixel(PngParts parts, ReadOnlySpan<byte> s, Span<byte> o)
    {
        switch (parts.Header.ColorType)
        {
            case 0: Set(o, s[0], s[0], s[0], KeyedAlpha(parts.Transparency, [s[0]])); break;
            case 2: Set(o, s[0], s[1], s[2], KeyedAlpha(parts.Transparency, s)); break;
            case 3: Palette(parts, s[0], o); break;
            case 4: Set(o, s[0], s[0], s[0], s[1]); break;
            default: s.CopyTo(o); break;
        }
    }

    private static void Palette(PngParts parts, byte index, Span<byte> o)
    {
        if (parts.Palette.Length == 0)
        {
            throw new InvalidPngException("a palette image has no PLTE chunk");
        }

        if ((index * 3) + 2 >= parts.Palette.Length)
        {
            throw new InvalidPngException($"palette index {index} is outside the PLTE chunk");
        }

        var alpha = index < parts.Transparency.Length ? parts.Transparency[index] : (byte)255;
        Set(o, parts.Palette[index * 3], parts.Palette[(index * 3) + 1], parts.Palette[(index * 3) + 2], alpha);
    }

    // tRNS for grey and RGB names one colour (16-bit samples, the low byte used at depth 8) that is fully transparent.
    private static byte KeyedAlpha(byte[] transparency, ReadOnlySpan<byte> samples)
    {
        if (transparency.Length != samples.Length * 2)
        {
            return 255;
        }

        for (var i = 0; i < samples.Length; i++)
        {
            if (transparency[(i * 2) + 1] != samples[i] || transparency[i * 2] != 0)
            {
                return 255;
            }
        }

        return 0;
    }

    private static void Set(Span<byte> o, byte r, byte g, byte b, byte a)
    {
        o[0] = r;
        o[1] = g;
        o[2] = b;
        o[3] = a;
    }
}
