namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A used and limit pair.</summary>
/// <param name="Used">Used, as served.</param>
/// <param name="Limit">Limit, as served; null when there is no cap.</param>
internal sealed record BudgetFigures(
    long Used,
    long? Limit);
