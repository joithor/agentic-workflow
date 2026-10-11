using System.IO.Compression;

namespace Aw.Evidence.Infrastructure.Images;

/// <summary>Inflates the IDAT data and undoes each scanline's filter (none, sub, up, average, Paeth).</summary>
internal static class PngScanlines
{
    /// <summary>The unfiltered scanlines, back to back (height * row length bytes).</summary>
    /// <param name="header">The image header.</param>
    /// <param name="compressed">The joined IDAT data.</param>
    /// <returns>The raw samples.</returns>
    /// <exception cref="InvalidPngException">The data doesn't inflate to the size the header promises, or a filter is unknown.</exception>
    internal static byte[] Decode(PngHeader header, byte[] compressed)
    {
        var stride = header.RowLength + 1;
        var filtered = Inflate(compressed, (long)stride * header.Height);
        var raw = new byte[header.RowLength * header.Height];
        for (var y = 0; y < header.Height; y++)
        {
            var row = raw.AsSpan(y * header.RowLength, header.RowLength);
            filtered.AsSpan((y * stride) + 1, header.RowLength).CopyTo(row);
            var prior = y == 0 ? [] : raw.AsSpan((y - 1) * header.RowLength, header.RowLength);
            Unfilter(filtered[y * stride], row, prior, header.Channels);
        }

        return raw;
    }

    private static byte[] Inflate(byte[] compressed, long expected)
    {
        var output = new byte[expected];
        try
        {
            using var zlib = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress);
            zlib.ReadExactly(output);
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
        {
            throw new InvalidPngException("the image data doesn't inflate to the size the header promises", ex);
        }

        return output;
    }

    private static void Unfilter(byte type, Span<byte> row, ReadOnlySpan<byte> prior, int bpp)
    {
        for (var i = 0; i < row.Length; i++)
        {
            int left = i >= bpp ? row[i - bpp] : 0, up = prior.IsEmpty ? 0 : prior[i], corner = i >= bpp && !prior.IsEmpty ? prior[i - bpp] : 0;
            row[i] += type switch
            {
                0 => 0,
                1 => (byte)left,
                2 => (byte)up,
                3 => (byte)((left + up) / 2),
                4 => (byte)Paeth(left, up, corner),
                _ => throw new InvalidPngException($"unknown scanline filter {type}"),
            };
        }
    }

    private static int Paeth(int left, int up, int corner)
    {
        var estimate = left + up - corner;
        int toLeft = Math.Abs(estimate - left), toUp = Math.Abs(estimate - up), toCorner = Math.Abs(estimate - corner);
        return toLeft <= toUp && toLeft <= toCorner ? left : toUp <= toCorner ? up : corner;
    }
}
