namespace Aw.Meta.Core.Lanes;

/// <summary>The last time one of the lane's hooks ran.</summary>
/// <param name="At">When.</param>
/// <param name="Stopped">Whether the hook was the stop check (the lane is idle); false for the inbox (a prompt, a session start or a tool call).</param>
public sealed record LaneBeat(DateTimeOffset At, bool Stopped);
