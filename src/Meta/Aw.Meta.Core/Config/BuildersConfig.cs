namespace Aw.Meta.Core.Config;

/// <summary>How many builder subagents a lane may run at once, and what each costs on disk; every field is optional.</summary>
/// <param name="MaxPerLane">The most builders per lane while the host has room.</param>
/// <param name="WorktreeGb">The disk one builder's worktree and build output take, in gigabytes.</param>
public sealed record BuildersConfig(int MaxPerLane = 4, int WorktreeGb = 4);
