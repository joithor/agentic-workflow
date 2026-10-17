using Aw.Kernel;
using Aw.Meta.Core.Health;

namespace Aw.Meta.Core.Ports;

/// <summary>
/// The heavy-job wrapper's two files in the state root: the slot cap (heavy-slots-cap, read only) and the reservation the meta
/// gates hold (heavy-meta-reserve, written here). The wrapper lets a job run with AW_HEAVY_PRIORITY=meta use any slot and every
/// other expensive job only the slots above the reservation.
/// </summary>
public interface IHeavyReserve
{
    /// <summary>The default cap when the cap file is missing or not a positive number.</summary>
    public const int DefaultCap = 3;

    /// <summary>Reads the cap and the current reservation (0 when the file is missing or unreadable; never above the cap).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The slots.</returns>
    public ValueTask<Result<HeavySlots>> Read(CancellationToken cancellationToken);

    /// <summary>Writes the reservation atomically (a temporary file renamed over the real one).</summary>
    /// <param name="slots">The slots to reserve.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or AW-META-010.</returns>
    public ValueTask<Result<Unit>> Write(int slots, CancellationToken cancellationToken);
}
