using Aw.Kernel;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core;

/// <summary>Records packet freezes under the packets/ state namespace (the sindri ledger takes over later, spec §4c).</summary>
public interface IFreezeLog
{
    /// <summary>Records <paramref name="record"/>; recording the same repository, packet and hash again changes nothing.</summary>
    /// <param name="record">The freeze.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Where it is recorded, relative to packets/ (FreezeLocation); AW-PACKETS-007 when it can't be written.</returns>
    public ValueTask<Result<FreezeReceipt>> Record(FreezeRecord record, CancellationToken cancellationToken);

    /// <summary>Finds the record of <paramref name="hash"/> for <paramref name="packet"/>, at <see cref="FreezeLocation"/>'s path.</summary>
    /// <param name="packet">The repository and packet id.</param>
    /// <param name="hash">The canonical hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The record; null when that hash was never frozen for that packet; AW-PACKETS-008 when the record exists but can't be read or parsed.</returns>
    public ValueTask<Result<FreezeRecord?>> Find(PacketIdentity packet, PacketHash hash, CancellationToken cancellationToken);
}
