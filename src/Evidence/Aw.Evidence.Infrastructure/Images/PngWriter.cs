using System.Buffers.Binary;
using System.IO.Compression;
using Aw.Evidence.Core.Images;

namespace Aw.Evidence.Infrastructure.Images;

/// <summary>Writes an RGBA image as an 8-bit colour type 6 PNG (rows unfiltered, zlib-compressed in one IDAT).</summary>
internal static class PngWriter
{
    /// <summary>The PNG file's bytes.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] Write(RgbaImage image)
    {
        using var file = new MemoryStream();
        file.Write(PngChunkReader.Signature);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, image.Width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], image.Height);
        header[8] = 8;   // bit depth
        header[9] = 6;   // RGBA; compression, filter and interlace stay 0
        Chunk(file, "IHDR"u8, header);
        Chunk(file, "IDAT"u8, Compress(image));
        Chunk(file, "IEND"u8, []);
        return file.ToArray();
    }

    private static byte[] Compress(RgbaImage image)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            var stride = image.Width * 4;
            for (var y = 0; y < image.Height; y++)
            {
                zlib.WriteByte(0);   // filter: none
                zlib.Write(image.Pixels.Slice(y * stride, stride));
            }
        }

        return compressed.ToArray();
    }

    private static void Chunk(MemoryStream file, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        file.Write(number);
        var typed = new byte[4 + data.Length];
        type.CopyTo(typed);
        data.CopyTo(typed.AsSpan(4));
        file.Write(typed);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc32.Compute(typed));
        file.Write(number);
    }
}
