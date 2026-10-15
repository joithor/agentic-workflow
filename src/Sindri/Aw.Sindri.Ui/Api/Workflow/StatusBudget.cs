namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The budget lines of a status.</summary>
/// <param name="Scope">Scope, as served.</param>
/// <param name="Used">Used, as served.</param>
/// <param name="Limit">Limit, as served; null when there is no cap.</param>
/// <param name="Instance">Instance, as served.</param>
internal sealed record StatusBudget(
    string Scope,
    long Used,
    long? Limit,
    BudgetFigures Instance);
