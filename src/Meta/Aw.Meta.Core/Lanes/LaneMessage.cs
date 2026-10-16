namespace Aw.Meta.Core.Lanes;

/// <summary>A message the meta sent to a non-Claude lane.</summary>
/// <param name="Seq">Its number, rising across the team.</param>
/// <param name="Lane">The lane it was sent to.</param>
/// <param name="Text">The text.</param>
/// <param name="At">When it was sent.</param>
/// <param name="Delivered">Whether the lane has been shown it (at a prompt or a stop).</param>
public sealed record LaneMessage(long Seq, string Lane, string Text, DateTimeOffset At, bool Delivered);
