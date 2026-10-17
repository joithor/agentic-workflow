using Aw.Kernel;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>Reads a line packet directory: the kind's listed files and its linked directories' files, plus the names of any other entries.</summary>
public interface ILinePacketSource
{
    /// <summary>Reads the packet in <paramref name="packetDirectory"/> as a <paramref name="kind"/> packet.</summary>
    /// <param name="kind">A line kind (<see cref="PacketKind.IsLine"/>).</param>
    /// <param name="packetDirectory">The packet directory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The files; AW-PACKETS-002 when the directory is missing; AW-PACKETS-003 when a file is a symbolic
    /// link or goes through one, or a linked directory holds more than <see cref="LineLimits.MaxLinkedFiles"/> files;
    /// AW-PACKETS-008 when one can't be read.</returns>
    public ValueTask<Result<LinePacketFiles>> Read(PacketKind kind, string packetDirectory, CancellationToken cancellationToken);
}
