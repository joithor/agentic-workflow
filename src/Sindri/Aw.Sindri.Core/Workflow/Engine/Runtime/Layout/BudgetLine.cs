namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>The token budgets of the state and the instance (E-23).</summary>
/// <param name="Scope">The scope; 'state'.</param>
/// <param name="Used">Tokens used.</param>
/// <param name="Limit">The cap, when the recipe declares one.</param>
/// <param name="Instance">The instance's budget.</param>
public sealed record BudgetLine(
    string Scope,
    long Used,
    long? Limit,
    InstanceBudget Instance);
