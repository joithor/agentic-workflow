namespace Aw.Meta.Core.Lanes;

/// <summary>One stop-check of a lane: who, what it owes, and the stop hook's state.</summary>
/// <param name="Identity">The lane.</param>
/// <param name="Owed">The messages the lane owes a reply to.</param>
/// <param name="HookActive">Whether the stop hook is already continuing a block.</param>
/// <param name="MaxBlocks">How many identical blocks in a row the lane gets before the next continuation is let through.</param>
/// <param name="Now">The current time.</param>
public sealed record LaneStop(LaneIdentity Identity, IReadOnlyList<LaneMessage> Owed, bool HookActive, int MaxBlocks, DateTimeOffset Now);
