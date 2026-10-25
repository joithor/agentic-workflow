namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The token budgets status shows (engine spec §3.7, §6.4 'budget'): spent against the state's and the instance's caps.</summary>
/// <param name="State">Tokens the active state has spent: this round's under the per-round rule, its lifetime total under the pooled one.</param>
/// <param name="StateCap">The state's budget, when it declares one.</param>
/// <param name="Instance">Tokens the instance has spent.</param>
/// <param name="InstanceCap">The recipe's budget, when it declares one.</param>
public sealed record BudgetView(int State, int? StateCap, int Instance, int? InstanceCap)
{
    /// <summary>Whether <see cref="State"/> counts this round only (the per-round rule).</summary>
    public bool PerRound { get; init; }

    /// <summary>The last round that spent past its allowance, as "round 1 used 139k of 100k"; null when none did.</summary>
    public string? Overrun { get; init; }
}
