using Aw.Kernel;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core;

/// <summary>Parses a holdout seal file, <c language="text">{schemaVersion, phase, setHash, planHash}</c> as Sindri writes it: strict JSON, schemaVersion 1, a 'sha256:' setHash. Pure: every ISealSource adapter supplies the bytes.</summary>
public static class SealReader
{
    /// <summary>Parses <paramref name="bytes"/>.</summary>
    /// <param name="name">The file's name, for messages.</param>
    /// <param name="bytes">The file.</param>
    /// <returns>The sealed set's hash, or AW-PACKETS-010 saying what is wrong.</returns>
    public static Result<PacketHash> Parse(string name, ReadOnlyMemory<byte> bytes) => throw new NotSupportedException();
}
