namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The token budgets status shows (engine spec §3.7, §6.4 'budget'): spent against the state's and the instance's caps.</summary>
/// <param name="State">Tokens the active state has spent.</param>
/// <param name="StateCap">The state's budget, when it declares one.</param>
/// <param name="Instance">Tokens the instance has spent.</param>
/// <param name="InstanceCap">The recipe's budget, when it declares one.</param>
public sealed record BudgetView(int State, int? StateCap, int Instance, int? InstanceCap);
