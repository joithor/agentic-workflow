namespace Aw.Packets.Core.Model;

/// <summary>A task's budget. Each limit is optional until the size-class budgets are defined.</summary>
public sealed record TaskBudget
{
    /// <summary>Model tokens.</summary>
    public long? Tokens { get; init; }

    /// <summary>Agent turns.</summary>
    public int? Turns { get; init; }

    /// <summary>Wall-clock minutes.</summary>
    public int? WallMinutes { get; init; }
}
