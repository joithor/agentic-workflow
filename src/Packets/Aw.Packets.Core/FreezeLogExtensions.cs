using Aw.Kernel;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core;

/// <summary>What 'freeze' reports after recording: the record that is stored, which for a hash frozen before is the first one (write-once).</summary>
public static class FreezeLogExtensions
{
    /// <summary>The record stored for <paramref name="requested"/>'s hash after <paramref name="receipt"/>.</summary>
    /// <param name="log">The freeze log.</param>
    /// <param name="requested">The record this freeze asked to write.</param>
    /// <param name="receipt">What <see cref="IFreezeLog.Record"/> said.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns><paramref name="requested"/> when it was written; otherwise the stored first record (AW-PACKETS-008 when it can't be read).</returns>
    public static async ValueTask<Result<FreezeRecord>> Stored(this IFreezeLog log, FreezeRecord requested, FreezeReceipt receipt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!receipt.AlreadyFrozen)
        {
            return requested;
        }

        var found = await log.Find(new PacketIdentity(requested.Repo, requested.Packet), requested.Hash, cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : found.Value ?? requested;
    }
}
