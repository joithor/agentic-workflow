using Aw.Kernel;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core;

/// <summary>Reads a packet directory: the manifest, the files it links and their bytes.</summary>
public interface IPacketSource
{
    /// <summary>Loads the packet in <paramref name="packetDirectory"/>.</summary>
    /// <param name="packetDirectory">The packet directory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The packet; AW-PACKETS-002 when the directory or its packet.json is missing; AW-PACKETS-003 when a
    /// file is malformed, missing or linked from outside the directory; AW-PACKETS-004 for an unsupported
    /// schemaVersion.</returns>
    public ValueTask<Result<LoadedPacket>> Load(string packetDirectory, CancellationToken cancellationToken);
}
