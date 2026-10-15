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
    public static LaneMapState Refresh(string team, IReadOnlyList<string> lanes, IReadOnlyList<LiveSession> live, IReadOnlyList<InboxMessage> announcements, LaneMapState previous)
    {
        ArgumentNullException.ThrowIfNull(lanes);
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(announcements);
        ArgumentNullException.ThrowIfNull(previous);
        var entries = lanes
            .Select(lane => Direct(team, lane, live) ?? Announced(lane, live, announcements))
            .OfType<LaneAssignment>()
            .ToList();
        return entries.SequenceEqual(previous.Entries)
            ? previous
            : new LaneMapState(previous.Version + 1, entries);
    }

    private static LaneAssignment? Direct(string team, string lane, IReadOnlyList<LiveSession> live)
    {
        var name = $"{team}-{lane}";
        return live.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) is { } session
            ? new LaneAssignment(lane, session.Name, session.Session, LaneSource.Direct)
            : null;
    }

    // The newest announcement of the lane by a session that is running now.
    private static LaneAssignment? Announced(string lane, IReadOnlyList<LiveSession> live, IReadOnlyList<InboxMessage> announcements)
    {
        foreach (var message in announcements
            .Where(m => m.Class.Kind == MessageKind.LaneAnnounce && string.Equals(m.Class.Lane, lane, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.At))
        {
            if (message.FromName.Length > 0
                && live.FirstOrDefault(s => string.Equals(s.Name, message.FromName, StringComparison.OrdinalIgnoreCase)) is { } session)
            {
                return new LaneAssignment(lane, session.Name, session.Session, LaneSource.Announcement);
            }
        }

        return null;
    }
}
