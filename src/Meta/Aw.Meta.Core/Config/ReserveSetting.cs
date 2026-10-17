using System.Text.Json.Serialization;

namespace Aw.Meta.Core.Config;

/// <summary>
/// The heavy-slot reservation: tiers from queue depth to reserved slots, or off. In team.json "reserve" is an array of
/// {"minDepth", "slots"} objects (slots is a number or "all"), false to leave the reservation file alone, or absent for the defaults.
/// </summary>
/// <param name="Enabled">Whether aw-meta writes the reservation file.</param>
/// <param name="Tiers">The tiers, in any order.</param>
[JsonConverter(typeof(ReserveSettingConverter))]
public sealed record ReserveSetting(bool Enabled, IReadOnlyList<ReserveTier> Tiers)
{
    /// <summary>One slot from three ready pull requests, two from five, all from seven.</summary>
    public static ReserveSetting Default { get; } = new(Enabled: true, [new ReserveTier(3, new ReserveSlots(1)), new ReserveTier(5, new ReserveSlots(2)), new ReserveTier(7, ReserveSlots.All)]);

    /// <summary>Never touch the reservation file.</summary>
    public static ReserveSetting Off { get; } = new(Enabled: false, []);

    /// <summary>The slots to reserve for a queue depth: the tier with the highest minDepth the depth reaches, else 0.</summary>
    /// <param name="depth">The ready pull requests not yet merged.</param>
    /// <param name="cap">The box-wide heavy cap.</param>
    /// <returns>The reservation, within 0 and the cap.</returns>
    public int For(int depth, int cap)
    {
        var reached = Tiers.Where(t => depth >= t.MinDepth).OrderByDescending(t => t.MinDepth).FirstOrDefault();
        return reached is null ? 0 : reached.Slots.Resolve(cap);
    }
}
