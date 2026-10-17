using System.Globalization;

namespace Aw.Meta.Core.Health;

/// <summary>The box-wide heavy slots and how many of them the meta gates should hold, with the queue depth behind it.</summary>
/// <param name="Cap">The slot count the reservation is sized against (heavy.cap in team.json).</param>
/// <param name="Reserve">The slots the meta gates should hold (0 to the cap).</param>
/// <param name="Depth">The merge-queue depth the reservation was computed from.</param>
/// <param name="ComputedAt">When it was computed.</param>
/// <param name="Stale">Whether it was computed long enough ago that the queue may have changed since.</param>
public sealed record HeavySlots(int Cap, int Reserve, int Depth, DateTimeOffset ComputedAt, bool Stale = false)
{
    /// <summary>The line to show: the value, its depth, and the command that applies it to the heavy-job wrapper.</summary>
    /// <returns>The line.</returns>
    public string Line() => string.Create(
        CultureInfo.InvariantCulture,
        $"heavy reservation: {Reserve} of {Cap} slots (queue depth {Depth}) - set it with meta-gate-reserve {Depth}{(Stale ? $" [stale: computed {ComputedAt:HH:mm}Z; run status or gate-run to recompute]" : string.Empty)}");
}
