namespace Aw.Meta.Core.Coordination;

/// <summary>What the stop guard decided.</summary>
/// <param name="Block">Whether the stop is blocked.</param>
/// <param name="Warning">A one-line warning when a stop is let through despite open obligations; null otherwise.</param>
public sealed record StopOutcome(bool Block, string? Warning);
