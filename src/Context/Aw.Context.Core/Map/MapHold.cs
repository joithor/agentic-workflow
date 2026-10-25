namespace Aw.Context.Core.Map;

/// <summary>A refreshed map waiting for the code index (tables map_hold and map_publish_chain): the draft that is held, since when the wait began, and the repos' commits it was built at.</summary>
/// <param name="Version">The held draft.</param>
/// <param name="Since">When the wait began: the first hold of the chain, kept across rebuilds (<see cref="NextSince"/>). A caller asking for a hold passes the time now.</param>
/// <param name="Heads">The commits the draft was built at (its map_version_head rows).</param>
public sealed record MapHold(int Version, DateTimeOffset Since, IReadOnlyList<MapHead> Heads)
{
    /// <summary>When a hold written at <paramref name="now"/> began: the chain's start if a wait is already under way, else now, and never later than now (a clock stepped back can't lengthen the wait).</summary>
    /// <param name="chain">The chain's stored start, or null when no wait is under way.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The start of the wait.</returns>
    public static DateTimeOffset NextSince(DateTimeOffset? chain, DateTimeOffset now) => chain is { } since && since < now ? since : now;
}
