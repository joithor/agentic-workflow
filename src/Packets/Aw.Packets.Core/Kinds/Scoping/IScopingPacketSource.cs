using Aw.Kernel;

namespace Aw.Packets.Core.Kinds.Scoping;

/// <summary>Reads a scope packet directory: only the files the scoping kind lists, plus the names of any others.</summary>
public interface IScopingPacketSource
{
    /// <summary>Reads the packet in <paramref name="packetDirectory"/>.</summary>
    /// <param name="packetDirectory">The packet directory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The files; AW-PACKETS-002 when the directory is missing; AW-PACKETS-003 when a listed file is a symbolic
    /// link; AW-PACKETS-008 when one can't be read.</returns>
    public ValueTask<Result<ScopingFiles>> Read(string packetDirectory, CancellationToken cancellationToken);
}
