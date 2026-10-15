using Aw.Meta.Core.Messages;

namespace Aw.Meta.Core.Lanes;

/// <summary>Builds the lane map from live sessions and lane announcements.</summary>
public static class LaneMap
{
    /// <summary>Maps each configured lane to a live session.</summary>
    /// <param name="team">The team name; a live session named "team-lane" maps directly.</param>
    /// <param name="lanes">The lane names.</param>
    /// <param name="live">The live sessions.</param>
    /// <param name="announcements">Lane announcement messages.</param>
    /// <param name="previous">The previous map, for the version.</param>
    /// <returns>The map; its version is the previous one unless the assignments changed.</returns>
    public static LaneMapState Refresh(string team, IReadOnlyList<string> lanes, IReadOnlyList<LiveSession> live, IReadOnlyList<InboxMessage> announcements, LaneMapState previous) =>
        throw new NotSupportedException("LaneMap: make LaneMapTests pass");
}
