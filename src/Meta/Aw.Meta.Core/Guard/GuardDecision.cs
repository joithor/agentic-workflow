namespace Aw.Meta.Core.Guard;

/// <summary>What the loop guard decided for one stop-check.</summary>
/// <param name="Block">Whether the stop is blocked.</param>
/// <param name="State">The state to remember.</param>
/// <param name="Allowed">Whether the stop is allowed only because the same set already blocked the maximum times.</param>
/// <param name="FirstAllowed">Whether this is the first such allowance, which is logged as an incident.</param>
public sealed record GuardDecision(bool Block, GuardState State, bool Allowed, bool FirstAllowed);
