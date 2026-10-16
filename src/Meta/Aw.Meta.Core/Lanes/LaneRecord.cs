using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Lanes;

/// <summary>What the launcher and the session-id hook left for a lane under lane-sessions.</summary>
/// <param name="Lane">The lane name.</param>
/// <param name="Session">The session id the lane's CLI reported; null before it did.</param>
/// <param name="Agent">The lane's agent facts; null for a lane the launcher recorded none for (a Claude lane from before the lane commands).</param>
/// <param name="RecordedAt">When the session id was recorded; null without one.</param>
/// <param name="Ambiguous">Whether the launcher refused to guess the id (more than one candidate).</param>
public sealed record LaneRecord(string Lane, SessionId? Session, LaneAgent? Agent, DateTimeOffset? RecordedAt = null, bool Ambiguous = false)
{
    /// <summary>Whether the lane runs a CLI other than Claude Code.</summary>
    public bool IsNonClaude => Agent is { IsClaude: false };
}
