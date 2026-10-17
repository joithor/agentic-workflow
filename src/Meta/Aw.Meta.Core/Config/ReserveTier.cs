namespace Aw.Meta.Core.Config;

/// <summary>From a merge-queue depth of <see cref="MinDepth"/>, the gates reserve <see cref="Slots"/> heavy slots.</summary>
/// <param name="MinDepth">The queue depth this tier starts at.</param>
/// <param name="Slots">The slots it reserves.</param>
public sealed record ReserveTier(int MinDepth, ReserveSlots Slots);
