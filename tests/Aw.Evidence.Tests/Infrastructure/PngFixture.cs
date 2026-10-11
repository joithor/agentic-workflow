using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>
/// Writes small PNG files for the decoder tests: any colour type, bit depth and interlace flag, optional PLTE and tRNS,
/// rows filtered with a chosen filter type. Independent of the codec under test (its own CRC and filters).
/// </summary>
internal sealed class PngFixture
{
    private readonly List<(string Type, byte[] Data)> _extra = [];

    internal required int Width { get; init; }

    internal required int Height { get; init; }

    internal required byte ColorType { get; init; }

    internal byte BitDepth { get; init; } = 8;

    internal byte Interlace { get; init; }

    /// <summary>The unfiltered scanlines, each Width * channels bytes.</summary>
    internal required byte[][] Rows { get; init; }

    /// <summary>The filter type applied to every row (0 none, 1 sub, 2 up, 3 average, 4 paeth), or -1 to cycle through all five.</summary>
    internal int Filter { get; init; }

    internal PngFixture With(string type, byte[] data)
    {
        _extra.Add((type, data));
        return this;
    }

    internal byte[] Build()
    {
        using var file = new MemoryStream();
        file.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Height);
        header[8] = BitDepth;
        header[9] = ColorType;
        header[12] = Interlace;
        Chunk(file, "IHDR", header);
        foreach (var (type, data) in _extra)
        {
            Chunk(file, type, data);
        }

        var image = Compress();
        Chunk(file, "IDAT", image[..(image.Length / 2)]);   // split, so the decoder must join IDAT chunks
        Chunk(file, "IDAT", image[(image.Length / 2)..]);
        Chunk(file, "IEND", []);
        return file.ToArray();
    }

    internal static uint Crc(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private byte[] Compress()
    {
        using var raw = new MemoryStream();
        using (var zlib = new ZLibStream(raw, CompressionLevel.Fastest, leaveOpen: true))
        {
            var bpp = Rows[0].Length / Width;
            for (var y = 0; y < Rows.Length; y++)
            {
                var type = Filter < 0 ? y % 5 : Filter;
                zlib.WriteByte((byte)type);
                zlib.Write(Filtered(type, Rows[y], y == 0 ? new byte[Rows[y].Length] : Rows[y - 1], bpp));
            }
        }

        return raw.ToArray();
    }

    private static byte[] Filtered(int type, byte[] row, byte[] prior, int bpp)
    {
        var output = new byte[row.Length];
        for (var i = 0; i < row.Length; i++)
        {
            int a = i >= bpp ? row[i - bpp] : 0, b = prior[i], c = i >= bpp ? prior[i - bpp] : 0;
            var predictor = type switch { 1 => a, 2 => b, 3 => (a + b) / 2, 4 => Paeth(a, b, c), _ => 0 };
            output[i] = (byte)(row[i] - predictor);
        }

        return output;
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void Chunk(Stream file, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        file.Write(number);
        var typed = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        file.Write(typed);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc(typed));
        file.Write(number);
    }
}
