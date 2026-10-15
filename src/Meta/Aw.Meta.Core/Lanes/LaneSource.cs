namespace Aw.Meta.Core.Lanes;

/// <summary>How a lane was matched to a session.</summary>
public enum LaneSource
{
    /// <summary>A live session named after the lane.</summary>
    Direct = 1,

    /// <summary>A live session that announced itself as the lane.</summary>
    Announcement = 2,
}
