namespace Aw.Evidence.Infrastructure.Images;

/// <summary>The CRC-32 PNG chunks carry (ISO 3309, polynomial 0xEDB88320, reflected), table driven.</summary>
internal static class Crc32
{
    private static readonly uint[] _table = BuildTable();

    /// <summary>The CRC of <paramref name="bytes"/>.</summary>
    /// <param name="bytes">The bytes (a chunk's type and data).</param>
    /// <returns>The CRC.</returns>
    internal static uint Compute(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc = _table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (var n = 0u; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
