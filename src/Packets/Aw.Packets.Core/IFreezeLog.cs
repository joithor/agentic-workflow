using Aw.Kernel;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core;

/// <summary>Records packet freezes under the packets/ state namespace (the sindri ledger takes over later, spec §4c).</summary>
public interface IFreezeLog
{
    /// <summary>Records <paramref name="record"/>; recording the same repository, packet and hash again changes nothing.</summary>
    /// <param name="record">The freeze.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Where it is recorded, relative to packets/ (FreezeLocation); AW-PACKETS-007 when it can't be written.</returns>
    public ValueTask<Result<FreezeReceipt>> Record(FreezeRecord record, CancellationToken cancellationToken);
}
