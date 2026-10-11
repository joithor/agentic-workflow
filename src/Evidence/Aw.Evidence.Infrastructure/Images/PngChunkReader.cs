using System.Buffers.Binary;
using System.Text;

namespace Aw.Evidence.Infrastructure.Images;

/// <summary>Walks a PNG's chunks, checking the signature, each length and CRC, and collects what a decode needs.</summary>
internal static class PngChunkReader
{
    /// <summary>The eight bytes every PNG starts with.</summary>
    internal static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Reads the chunks up to IEND.</summary>
    /// <param name="png">The file's bytes.</param>
    /// <returns>The parts.</returns>
    /// <exception cref="InvalidPngException">The file is not a well-formed PNG.</exception>
    internal static PngParts Read(ReadOnlySpan<byte> png)
    {
        if (!png.StartsWith(Signature))
        {
            throw new InvalidPngException("not a PNG (the signature is missing)");
        }

        PngHeader? header = null;
        byte[] palette = [], transparency = [];
        using var compressed = new MemoryStream();
        var rest = png[Signature.Length..];
        while (true)
        {
            var type = Next(ref rest, out var data);
            switch (type)
            {
                case "IHDR": header = PngHeader.Parse(data); break;
                case "PLTE": palette = data.ToArray(); break;
                case "tRNS": transparency = data.ToArray(); break;
                case "IDAT": compressed.Write(data); break;
                case "IEND": return new(header ?? throw new InvalidPngException("IHDR is missing"), palette, transparency, compressed.ToArray());
                default: break;   // ancillary chunks (gAMA, sRGB, pHYs, text) don't change the pixels this decoder reports
            }
        }
    }

    private static string Next(ref ReadOnlySpan<byte> rest, out ReadOnlySpan<byte> data)
    {
        if (rest.Length < 12)
        {
            throw new InvalidPngException("the file ends before IEND (truncated)");
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(rest);
        if (length > (uint)(rest.Length - 12))
        {
            throw new InvalidPngException("a chunk runs past the end of the file (truncated)");
        }

        var typed = rest.Slice(4, 4 + (int)length);
        var crc = BinaryPrimitives.ReadUInt32BigEndian(rest[(8 + (int)length)..]);
        var type = Encoding.ASCII.GetString(typed[..4]);
        if (Crc32.Compute(typed) != crc)
        {
            throw new InvalidPngException($"the {type} chunk's CRC doesn't match (corrupt file)");
        }

        rest = rest[(12 + (int)length)..];
        data = typed[4..];
        return type;
    }
}
